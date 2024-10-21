using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Diginsight.Analyzer.Business;

internal sealed partial class AgentExecutionService : IAgentExecutionService
{
    private readonly ILogger logger;
    private readonly IAmbientService ambientService;
    private readonly IAgentLeaseService leaseService;
    private readonly IHostApplicationLifetime applicationLifetime;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly IAgentParallelismSettingsAccessor parallelismSettingsAccessor;
    private readonly IAgentEventMetaAccessor eventMetaAccessor;

    private readonly SemaphoreSlim semaphore = new (1, 1);

    private bool isShuttingDown = false;
    private (CancellationTokenSource CancellationSource, ExecutionKind Kind, Guid InstanceId, ManualResetEventSlim Mre)? current;

    public AgentExecutionService(
        ILogger<AgentExecutionService> logger,
        IAmbientService ambientService,
        IAgentLeaseService leaseService,
        IHostApplicationLifetime applicationLifetime,
        IServiceScopeFactory serviceScopeFactory,
        IAgentParallelismSettingsAccessor parallelismSettingsAccessor,
        IAgentEventMetaAccessor eventMetaAccessor
    )
    {
        this.logger = logger;
        this.ambientService = ambientService;
        this.leaseService = leaseService;
        this.applicationLifetime = applicationLifetime;
        this.serviceScopeFactory = serviceScopeFactory;
        this.parallelismSettingsAccessor = parallelismSettingsAccessor;
        this.eventMetaAccessor = eventMetaAccessor;
    }

    public async Task<Guid> StartAsync<TLease>(
        ExecutionKind kind,
        Guid? requestedInstanceId,
        IEnumerable<Guid> siteIds,
        Action<TLease> fillLease,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        Func<Guid, CancellationToken, Task> coreStartAsync,
        CancellationToken cancellationToken
    )
        where TLease : ActiveLease, new()
    {
        using IDisposable? d0 = new LogScopeBuilder().With("ExecutionKind", kind).Begin(logger);
        using IDisposable? d1 = requestedInstanceId is { } instanceId
            ? new LogScopeBuilder().With("InstanceId", instanceId).Begin(logger)
            : null;

        instanceId = await LockExecutionAsync(kind, requestedInstanceId, siteIds, fillLease, hasConflictAsync, cancellationToken);

        using IDisposable? d2 = new LogScopeBuilder().With("InstanceId", instanceId).Begin(logger);
        LogMessages.ExecutionStarted(logger);

        bool faulted = true;
        bool cancelled = false;
        try
        {
            await coreStartAsync(instanceId, cancellationToken);
            faulted = false;

            return instanceId;
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            throw;
        }
        finally
        {
            if (faulted)
            {
                await UnlockExecutionAsync(cancelled);
            }
        }
    }

    public async Task<IEnumerable<Guid>> AbortAsync(ExecutionKind kind, Guid? instanceId)
    {
        await semaphore.WaitAsync();
        try
        {
            if (current is var (cancellationSource, kind0, instanceId0, _) && kind0 == kind && instanceId0 == (instanceId ?? instanceId0))
            {
                cancellationSource.Cancel();
                return new[] { instanceId0 };
            }
            else
            {
                return Enumerable.Empty<Guid>();
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    public Task RunDetachedAsync<TServices>(
        Func<IServiceProvider, TServices> getServices,
        Func<TServices, CancellationToken, Task> runAsync,
        CancellationToken cancellationToken
    )
    {
        IParallelismSettings clonedParallelismSettings = parallelismSettingsAccessor.Get().Clone();
        IReadOnlyDictionary<string, IEnumerable<string>> clonedEventMeta = eventMetaAccessor.Get()
            .ToDictionary(static x => x.Key, static x => x.Value.ToArray().AsEnumerable());

        TaskCompletionSource tcs = new (TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(
            async () =>
            {
                using IServiceScope serviceScope = serviceScopeFactory.CreateScope();
                IServiceProvider scopeServiceProvider = serviceScope.ServiceProvider;

                TServices services;
                try
                {
                    parallelismSettingsAccessor.Set(clonedParallelismSettings);
                    eventMetaAccessor.Set(clonedEventMeta);

                    services = getServices(scopeServiceProvider);
                }
                catch (Exception exception)
                {
                    tcs.SetException(exception);
                    return;
                }

                tcs.SetResult();

                bool cancelled = false;
                try
                {
                    await runAsync(services, current!.Value.CancellationSource.Token);
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                catch (Exception exception)
                {
                    LogMessages.UnexpectedErrorDuringExecution(logger, exception);
                }
                finally
                {
                    await UnlockExecutionAsync(cancelled);
                }
            },
            cancellationToken
        );

        return tcs.Task;
    }

    public async Task WaitForFinishAsync()
    {
        ManualResetEventSlim? mre;

        await semaphore.WaitAsync();
        try
        {
            isShuttingDown = true;
            mre = current?.Mre;
        }
        finally
        {
            semaphore.Release();
        }

        // ReSharper disable once MethodSupportsCancellation
        mre?.Wait();
    }

    public async Task<(ExecutionKind Kind, Guid InstanceId)?> GetCurrentAsync()
    {
        await semaphore.WaitAsync();
        try
        {
            return current is var (_, kind, instanceId, _) ? (kind, instanceId) : null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task<Guid> LockExecutionAsync<TLease>(
        ExecutionKind kind,
        Guid? requestedInstanceId,
        IEnumerable<Guid> siteIds,
        Action<TLease> fillLease,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        CancellationToken cancellationToken
    )
        where TLease : ActiveLease, new()
    {
        LogMessages.LockingExecution(logger);

        // ReSharper disable once MethodSupportsCancellation
        await semaphore.WaitAsync();
        try
        {
            if (isShuttingDown)
            {
                LogMessages.ShuttingDown(logger);
                throw new MigrationException("Shutting down", HttpStatusCode.ServiceUnavailable, "ShuttingDown");
            }

            if (current is var (_, otherKind, otherInstanceId, _))
            {
                LogMessages.AlreadyExecuting(logger, otherKind, otherInstanceId);
                throw MigrationExceptions.AlreadyExecuting(otherKind, otherInstanceId);
            }

            Guid instanceId = requestedInstanceId ?? ambientService.NewGuid();

            await leaseService.AcquireExecutionAsync(instanceId, siteIds, fillLease, hasConflictAsync, cancellationToken);

            current = (
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, applicationLifetime.ApplicationStopping),
                kind,
                instanceId,
                new ManualResetEventSlim());

            return instanceId;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private async Task UnlockExecutionAsync(bool cancelled)
    {
        await semaphore.WaitAsync();
        try
        {
            await leaseService.ReleaseExecutionAsync();

            using (ManualResetEventSlim mre = current!.Value.Mre)
            {
                mre.Set();
            }

            if (cancelled)
            {
                LogMessages.ExecutionCancelled(logger);
            }
            else
            {
                LogMessages.ExecutionFinished(logger);
            }

            current = null;
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Locking execution")]
        internal static partial void LockingExecution(ILogger logger);

        [LoggerMessage(1, LogLevel.Warning, "Already executing {Kind} instance {InstanceId}")]
        internal static partial void AlreadyExecuting(ILogger logger, ExecutionKind kind, Guid instanceId);

        [LoggerMessage(2, LogLevel.Information, "Execution started")]
        internal static partial void ExecutionStarted(ILogger logger);

        [LoggerMessage(3, LogLevel.Information, "Execution finished")]
        internal static partial void ExecutionFinished(ILogger logger);

        [LoggerMessage(4, LogLevel.Information, "Execution cancelled")]
        internal static partial void ExecutionCancelled(ILogger logger);

        [LoggerMessage(5, LogLevel.Error, "Unexpected error during execution")]
        internal static partial void UnexpectedErrorDuringExecution(ILogger logger, Exception exception);

        [LoggerMessage(6, LogLevel.Information, "Shutting down")]
        internal static partial void ShuttingDown(ILogger logger);

        [LoggerMessage(7, LogLevel.Warning, "Duplicate instance id")]
        internal static partial void DuplicateInstanceId(ILogger logger);
    }
}
