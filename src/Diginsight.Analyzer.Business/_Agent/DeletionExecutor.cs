using Microsoft.Extensions.Logging;

namespace Diginsight.Analyzer.Business;

internal partial class DeletionExecutor : IDeletionExecutor
{
    private readonly IAmbientService ambientService;
    private readonly IFileLoggerFactoryFactory fileLoggerFactoryFactory;
    private readonly ILoggerFactorySetter loggerFactorySetter;
    private readonly ICoreDeletionExecutor coreDeletionExecutor;
    private readonly IParallelismSettings parallelismSettings;
    private readonly IEventService eventService;

    public DeletionExecutor(
        IAmbientService ambientService,
        IFileLoggerFactoryFactory fileLoggerFactoryFactory,
        ILoggerFactorySetter loggerFactorySetter,
        ICoreDeletionExecutor coreDeletionExecutor,
        IParallelismSettings parallelismSettings,
        IEventService eventService
    )
    {
        this.ambientService = ambientService;
        this.fileLoggerFactoryFactory = fileLoggerFactoryFactory;
        this.loggerFactorySetter = loggerFactorySetter;
        this.coreDeletionExecutor = coreDeletionExecutor;
        this.parallelismSettings = parallelismSettings;
        this.eventService = eventService;
    }

    public async Task ExecuteAsync(
        Guid instanceId,
        IEnumerable<Guid> siteIds,
        DeletionMode mode,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    )
    {
        DateTime startedAt = ambientService.UtcNow;

        using ILoggerFactory fileLoggerFactory = fileLoggerFactoryFactory.MakeGlobal(startedAt, instanceId);
        using IDisposable d0 = loggerFactorySetter.WithLoggerFactory(fileLoggerFactory);
        ILogger logger = loggerFactorySetter.CreateLogger<DeletionExecutor>();

        await Parallel.ForEachAsync(
            siteIds,
            new ParallelOptions() { CancellationToken = cancellationToken, MaxDegreeOfParallelism = parallelismSettings.GetForExecution(ExecutionKind.Deletion) },
            async (siteId, ct) =>
            {
                using IDisposable? d1 = new LogScopeBuilder().With("SiteId", siteId).Begin(logger);
                LogMessages.ProcessingSite(logger);

                await eventService.EmitAsync(
                    eventRecipients,
                    (m, t) => new DeletionStartedEvent()
                    {
                        InstanceId = instanceId,
                        Timestamp = t,
                        Metadata = m,
                        SiteId = siteId,
                        Queued = false,
                    }
                );

                bool succeeded;
                try
                {
                    succeeded = await coreDeletionExecutor.ExecuteAsync(siteId, mode, ct);
                    if (succeeded)
                    {
                        LogMessages.SiteProcessedSuccessfully(logger);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    succeeded = false;
                    LogMessages.SiteFailedAbruptly(logger, exception);
                }

                await eventService.EmitAsync(
                    eventRecipients,
                    (m, t) => new DeletionFinishedEvent()
                    {
                        InstanceId = instanceId,
                        Timestamp = t,
                        Metadata = m,
                        SiteId = siteId,
                        Succeeded = succeeded,
                    }
                );
            }
        );
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Error, "Site failed abruptly")]
        internal static partial void SiteFailedAbruptly(ILogger logger, Exception exception);

        [LoggerMessage(1, LogLevel.Debug, "Processing site")]
        internal static partial void ProcessingSite(ILogger logger);

        [LoggerMessage(2, LogLevel.Debug, "Site processed successfully")]
        internal static partial void SiteProcessedSuccessfully(ILogger logger);
    }
}
