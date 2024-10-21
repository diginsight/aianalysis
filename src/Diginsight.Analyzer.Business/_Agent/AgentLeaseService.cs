using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Timer = System.Timers.Timer;

namespace Diginsight.Analyzer.Business;

internal sealed partial class AgentLeaseService : IAgentLeaseService
{
    private readonly ILogger logger;
    private readonly IAgentAmbientService ambientService;
    private readonly IRepository<Lease> leaseRepository;
    private readonly SemaphoreSlim semaphore = new (1, 1);
    private readonly IOptionsMonitor<CoreConfig> coreConfigMonitor;

    private Lease? lease;
    private IDisposable? keepaliveDisposable;

    public AgentLeaseService(
        ILogger<AgentLeaseService> logger,
        IAgentAmbientService ambientService,
        IRepository<Lease> leaseRepository,
        IOptionsMonitor<CoreConfig> coreConfigMonitor
    )
    {
        this.logger = logger;
        this.ambientService = ambientService;
        this.leaseRepository = leaseRepository;
        this.coreConfigMonitor = coreConfigMonitor;
    }

    private IAgentCoreConfig CoreConfig => coreConfigMonitor.CurrentValue;

    public Task CreateAsync()
    {
        int ttlMinutes = Math.Max(CoreConfig.LeaseTtlMinutes, 1);

        return WithSemaphore(
            async () =>
            {
                LogMessages.CreatingLease(logger);

                lease = new Lease()
                {
                    BaseAddress = ambientService.BaseAddress,
                    MachineName = ambientService.MachineName,
                    Family = ambientService.Family,
                    TtlSeconds = (int)TimeSpan.FromMinutes(ttlMinutes).TotalSeconds,
                };
                await leaseRepository.AddItemAsync(lease);

                Timer timer = new (TimeSpan.FromMinutes(ttlMinutes - 0.5).TotalMilliseconds) { AutoReset = true };

                async Task KeepaliveAsync()
                {
                    await WithSemaphore(
                        async () =>
                        {
                            try
                            {
                                if (lease is null)
                                {
                                    return;
                                }

                                await leaseRepository.UpsertItemAsync(lease);
                            }
                            catch (Exception e)
                            {
                                _ = e;
                            }
                        }
                    );
                }

                timer.Elapsed += (_, _) => { KeepaliveAsync().GetAwaiter().GetResult(); };
                timer.Start();

                keepaliveDisposable = timer;
            }
        );
    }

    public Task AcquireExecutionAsync<TLease>(
        Guid instanceId,
        IEnumerable<Guid> siteIds,
        Action<TLease> fillLease,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        CancellationToken cancellationToken
    )
        where TLease : ActiveLease, new()
    {
        return WithSemaphore(
            async () =>
            {
                LogMessages.AcquiringTemporaryExecution(logger);

                string leaseId = lease!.Id;

                TLease newLease = lease.As<TLease>();
                newLease.InstanceId = instanceId;
                newLease.SiteIds = siteIds;
                fillLease(newLease);

                // ReSharper disable once MethodSupportsCancellation
                await leaseRepository.UpsertItemAsync(newLease);

                await foreach (ActiveLease otherLease in leaseRepository
                    .GetItemsAE(q => q.Where(x => x.Kind != null && x.Id != leaseId), cancellationToken)
                    .Select(static x => x.AsActive()!)
                    .WithCancellation(cancellationToken))
                {
                    if (!await hasConflictAsync(otherLease, cancellationToken))
                    {
                        continue;
                    }

                    ExecutionKind otherKind = otherLease.Kind!.Value;
                    Guid otherInstanceId = otherLease.InstanceId;

                    LogMessages.ConflictingExecution(logger, otherKind, otherInstanceId);

                    // ReSharper disable once MethodSupportsCancellation
                    await leaseRepository.UpsertItemAsync(lease);

                    throw MigrationExceptions.ConflictingExecution(otherKind, otherInstanceId);
                }

                LogMessages.ExecutionConfirmed(logger);

                lease = newLease;
            }
        );
    }

    public Task ReleaseExecutionAsync()
    {
        return WithSemaphore(
            () =>
            {
                LogMessages.ReleasingExecution(logger);

                lease = new Lease(lease!);

                return leaseRepository.UpsertItemAsync(lease);
            }
        );
    }

    public Task DeleteAsync()
    {
        return WithSemaphore(
            async () =>
            {
                LogMessages.DeletingLease(logger);

                if (keepaliveDisposable is not null)
                {
                    keepaliveDisposable.Dispose();
                    keepaliveDisposable = null;
                }

                if (lease is not null)
                {
                    await leaseRepository.DeleteItemAsync(lease.Id, new PartitionKey(lease.Id));
                    lease = null;
                }
            }
        );
    }

    // ReSharper disable once AsyncApostle.AsyncMethodNamingHighlighting
    private async Task WithSemaphore(Func<Task> runAsync)
    {
        await semaphore.WaitAsync();
        try
        {
            await runAsync();
        }
        finally
        {
            semaphore.Release();
        }
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Creating lease")]
        internal static partial void CreatingLease(ILogger logger);

        [LoggerMessage(1, LogLevel.Debug, "Acquiring temporary execution")]
        internal static partial void AcquiringTemporaryExecution(ILogger logger);

        [LoggerMessage(2, LogLevel.Warning, "Found conflicting {Kind} execution with id {InstanceId}")]
        internal static partial void ConflictingExecution(ILogger logger, ExecutionKind kind, Guid instanceId);

        [LoggerMessage(3, LogLevel.Debug, "Execution confirmed")]
        internal static partial void ExecutionConfirmed(ILogger logger);

        [LoggerMessage(4, LogLevel.Debug, "Releasing execution")]
        internal static partial void ReleasingExecution(ILogger logger);

        [LoggerMessage(5, LogLevel.Debug, "Deleting lease")]
        internal static partial void DeletingLease(ILogger logger);
    }
}
