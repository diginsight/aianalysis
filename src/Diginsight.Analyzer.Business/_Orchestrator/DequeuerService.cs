using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diginsight.Analyzer.Business;

internal sealed partial class DequeuerService : BackgroundService, IDequeuerService
{
    private readonly ILogger logger;
    private readonly IMigrationInfoRepository migrationInfoRepository;
    private readonly IOrchestratorExecutionService executionService;
    private readonly IServiceScopeFactory serviceScopeFactory;
    private readonly IParallelismSettingsAccessor parallelismSettingsAccessor;
    private readonly IEventMetaAccessor eventMetaAccessor;
    private readonly IOptionsMonitor<CoreConfig> coreConfigMonitor;

    private CancellationTokenSource? loopCancellationTokenSource;

    public DequeuerService(
        ILogger<DequeuerService> logger,
        IMigrationInfoRepository migrationInfoRepository,
        IOrchestratorExecutionService executionService,
        IServiceScopeFactory serviceScopeFactory,
        IParallelismSettingsAccessor parallelismSettingsAccessor,
        IEventMetaAccessor eventMetaAccessor,
        IOptionsMonitor<CoreConfig> coreConfigMonitor
    )
    {
        this.logger = logger;
        this.migrationInfoRepository = migrationInfoRepository;
        this.executionService = executionService;
        this.serviceScopeFactory = serviceScopeFactory;
        this.parallelismSettingsAccessor = parallelismSettingsAccessor;
        this.eventMetaAccessor = eventMetaAccessor;
        this.coreConfigMonitor = coreConfigMonitor;
    }

    private IOrchestratorCoreConfig CoreConfig => coreConfigMonitor.CurrentValue;

    public void TriggerDequeue()
    {
        loopCancellationTokenSource?.Cancel();
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            LogMessages.LookingForQueuedMigrations(logger);

            int failures = 0;

            try
            {
                IAsyncEnumerable<MigrationContextSnapshot> snapshots = migrationInfoRepository.GetAllQueuedMigrationSnapshotsAE();
                await foreach (MigrationContextSnapshot snapshot in snapshots.WithCancellation(CancellationToken.None))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool proceed;
                    Guid instanceId = snapshot.InstanceId;
                    using (new LogScopeBuilder().With("InstanceId", instanceId).Begin(logger))
                    {
                        LogMessages.QueuedMigrationFound(logger);

                        using IServiceScope serviceScope = serviceScopeFactory.CreateScope();

                        try
                        {
                            proceed = await executionService.DequeueAsync(instanceId, snapshot.Family, cancellationToken);
                        }
                        catch (Exception exception)
                        {
                            if (exception is not MigrationException { Label: nameof(MigrationExceptions.ConflictingExecution) })
                            {
                                LogMessages.ErrorInvokingAgent(logger, exception);
                            }

                            failures += 1;
                            proceed = failures < CoreConfig.DequeuerMaxFailures;
                        }
                    }

                    if (!proceed)
                    {
                        LogMessages.SuspendingLoop(logger);
                        break;
                    }
                }
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken == cancellationToken) { }
            catch (Exception exception)
            {
                LogMessages.UnexpectedErrorDuringDequeue(logger, exception);
            }

            loopCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(CoreConfig.DequeuerIntervalSeconds), loopCancellationTokenSource.Token);
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken != cancellationToken) { }
            finally
            {
                loopCancellationTokenSource = null;
            }
        }
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Warning, "Error invoking agent")]
        internal static partial void ErrorInvokingAgent(ILogger logger, Exception exception);

        [LoggerMessage(1, LogLevel.Error, "Unexpected error during dequeue")]
        internal static partial void UnexpectedErrorDuringDequeue(ILogger logger, Exception exception);

        [LoggerMessage(2, LogLevel.Debug, "Looking for queued migrations")]
        internal static partial void LookingForQueuedMigrations(ILogger logger);

        [LoggerMessage(3, LogLevel.Information, "Queued migration found")]
        internal static partial void QueuedMigrationFound(ILogger logger);

        [LoggerMessage(4, LogLevel.Debug, "Suspending queued lookup loop")]
        internal static partial void SuspendingLoop(ILogger logger);
    }
}
