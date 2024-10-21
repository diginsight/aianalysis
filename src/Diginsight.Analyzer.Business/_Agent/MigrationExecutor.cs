using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed partial class MigrationExecutor : IMigrationExecutor
{
    private readonly IAmbientService ambientService;
    private readonly IAgentMigrationContextFactory migrationContextFactory;
    private readonly IMigrationInfoRepository migrationInfoRepository;
    private readonly IFileLoggerFactoryFactory fileLoggerFactoryFactory;
    private readonly ILoggerFactorySetter loggerFactorySetter;
    private readonly IParallelismSettings parallelismSettings;
    private readonly IEventService eventService;
    private readonly IPhaseContextVisitor<Task, (IEnumerable<string> Recipients, StepHistory StepHistory)> stepStartedEventEmitter;
    private readonly IPhaseContextVisitor<Task, (IEnumerable<string> Recipients, StepHistory StepHistory)> stepFinishedEventEmitter;

    public MigrationExecutor(
        IAmbientService ambientService,
        IAgentMigrationContextFactory migrationContextFactory,
        IMigrationInfoRepository migrationInfoRepository,
        IFileLoggerFactoryFactory fileLoggerFactoryFactory,
        ILoggerFactorySetter loggerFactorySetter,
        IParallelismSettings parallelismSettings,
        IEventService eventService
    )
    {
        this.ambientService = ambientService;
        this.migrationContextFactory = migrationContextFactory;
        this.migrationInfoRepository = migrationInfoRepository;
        this.fileLoggerFactoryFactory = fileLoggerFactoryFactory;
        this.loggerFactorySetter = loggerFactorySetter;
        this.parallelismSettings = parallelismSettings;
        this.eventService = eventService;

        stepStartedEventEmitter = new StepStartedEventEmitter(eventService);
        stepFinishedEventEmitter = new StepFinishedEventEmitter(eventService);
    }

    public async Task ExecuteAsync(
        Guid instanceId,
        DateTime? queuedAt,
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<IGlobalMigratorStepExecutor> globalMigratorStepExecutors,
        IEnumerable<ISiteMigratorStepExecutor> siteMigratorStepExecutors,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    )
    {
        IMigrationContext migrationContext = migrationContextFactory.Make(
            instanceId,
            globalInfo,
            sites,
            globalMigratorStepExecutors.Select(static x => x.Name),
            siteMigratorStepExecutors.Select(static x => x.Name),
            queuedAt
        );
        DateTime startedAt = migrationContext.StartedAt;

        await migrationInfoRepository.InsertAsync(migrationContext);
        await eventService.EmitAsync(
            eventRecipients,
            (m, _) => new MigrationStartedEvent()
            {
                InstanceId = instanceId,
                Timestamp = startedAt,
                Metadata = m,
                Queued = queuedAt is not null,
            }
        );

        try
        {
            await WithTimeBoundAsync(CoreExecuteAsync, migrationContext, migrationContext, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            migrationContext.Exception = exception;
        }
        finally
        {
            await migrationInfoRepository.UpsertAsync(migrationContext);
            await eventService.EmitAsync(
                eventRecipients,
                (m, _) => new MigrationFinishedEvent()
                {
                    InstanceId = instanceId,
                    Timestamp = migrationContext.FinishedAt!.Value,
                    Metadata = m,
                    Status = migrationContext.IsFailed ? FinishedEventStatus.Failed
                        : migrationContext.Status == TimeBoundStatus.Aborted ? FinishedEventStatus.Aborted
                        : FinishedEventStatus.Completed,
                }
            );
        }

        async Task CoreExecuteAsync(CancellationToken ct)
        {
            using (ILoggerFactory fileLoggerFactory = fileLoggerFactoryFactory.MakeGlobal(startedAt, instanceId))
            using (loggerFactorySetter.WithLoggerFactory(fileLoggerFactory))
            {
                foreach (IGlobalMigratorStepExecutor globalMigratorStepExecutor in globalMigratorStepExecutors)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!migrationContext.IsSucceeded())
                    {
                        return;
                    }

                    await ExecuteGlobalAsync(globalMigratorStepExecutor, migrationContext, loggerFactorySetter, eventRecipients, false, ct);
                }
            }

            foreach (ISiteMigratorStepExecutor siteMigratorStepExecutor in siteMigratorStepExecutors)
            {
                ct.ThrowIfCancellationRequested();

                await ExecuteSitesAsync(siteMigratorStepExecutor, migrationContext, eventRecipients, false, ct);
            }

            foreach (ISiteMigratorStepExecutor siteMigratorStepExecutor in siteMigratorStepExecutors.Reverse())
            {
                ct.ThrowIfCancellationRequested();

                await ExecuteSitesAsync(siteMigratorStepExecutor, migrationContext, eventRecipients, true, ct);
            }

            using (ILoggerFactory fileLoggerFactory = fileLoggerFactoryFactory.MakeGlobal(startedAt, instanceId))
            using (loggerFactorySetter.WithLoggerFactory(fileLoggerFactory))
            {
                foreach (IGlobalMigratorStepExecutor globalMigratorStepExecutor in globalMigratorStepExecutors.Reverse())
                {
                    ct.ThrowIfCancellationRequested();

                    if (!migrationContext.IsSucceeded())
                    {
                        return;
                    }

                    await ExecuteGlobalAsync(globalMigratorStepExecutor, migrationContext, loggerFactorySetter, eventRecipients, true, ct);
                }
            }
        }
    }

    private Task ExecuteGlobalAsync(
        IGlobalMigratorStepExecutor globalMigratorStepExecutor,
        IMigrationContext migrationContext,
        ILoggerFactory fileLoggerFactory,
        IEnumerable<string> eventRecipients,
        bool isAfter,
        CancellationToken cancellationToken
    )
    {
        Action<ILogger> log;
        Func<IMigrationContext, CancellationToken, Task> executeAsync;
        if (isAfter)
        {
            log = LogMessages.RunningGlobalMigratorStepAfter;
            executeAsync = globalMigratorStepExecutor.ExecuteAfterAsync;
        }
        else
        {
            log = LogMessages.RunningGlobalMigratorStep;
            executeAsync = globalMigratorStepExecutor.ExecuteAsync;
        }

        ILogger fileLogger = fileLoggerFactory.CreateLogger(globalMigratorStepExecutor.GetType());
        log(fileLogger);

        return WithStepHistoryAsync(
            ct => executeAsync(migrationContext, ct),
            migrationContext,
            globalMigratorStepExecutor,
            eventRecipients,
            cancellationToken
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task ExecuteSitesAsync(
        ISiteMigratorStepExecutor siteMigratorStepExecutor,
        IMigrationContext migrationContext,
        IEnumerable<string> eventRecipients,
        bool isAfter,
        CancellationToken cancellationToken
    )
    {
        Action<ILogger> log;
        Func<ISiteContext, CancellationToken, Task> executeAsync;
        if (isAfter)
        {
            log = LogMessages.RunningSiteMigratorStepAfter;
            executeAsync = siteMigratorStepExecutor.ExecuteAfterAsync;
        }
        else
        {
            log = LogMessages.RunningSiteMigratorStep;
            executeAsync = siteMigratorStepExecutor.ExecuteAsync;
        }

        Guid instanceId = migrationContext.InstanceId;
        DateTime startedAt = migrationContext.StartedAt;

        IEnumerable<(ISiteContext SiteContext, Func<ILoggerFactory> MakeFileLoggerFactory)> siteTuples = migrationContext
            .GetSiteContexts()
            .Where(static x => x.IsSucceeded())
            .Select(x => ((ISiteContext, Func<ILoggerFactory>))(x, () => fileLoggerFactoryFactory.MakeSite(startedAt, instanceId, x.SiteId)))
            .ToArray();

        return Parallel.ForEachAsync(
            siteTuples,
            new ParallelOptions()
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = parallelismSettings.GetForExecution(ExecutionKind.Migration),
            },
            async (siteTuple, ct) =>
            {
                (ISiteContext siteContext, Func<ILoggerFactory> makeFileLoggerFactory) = siteTuple;

                using ILoggerFactory fileLoggerFactory = makeFileLoggerFactory();
                using IDisposable d0 = loggerFactorySetter.WithLoggerFactory(fileLoggerFactory);

                ILogger fileLogger = loggerFactorySetter.CreateLogger(siteMigratorStepExecutor.GetType());
                log(fileLogger);

                await WithStepHistoryAsync(
                    ct0 => executeAsync(siteContext, ct0),
                    siteContext,
                    siteMigratorStepExecutor,
                    eventRecipients,
                    ct
                );
            }
        );
    }

    private async Task WithTimeBoundAsync(
        Func<CancellationToken, Task> runAsync, ITimeBound timeBound, IPhaseContext phaseContext, CancellationToken cancellationToken
    )
    {
        bool isPartial = timeBound is ITimeBoundWithAfter { FinishedAt: null };
        TimeBoundStatus completedStatus = isPartial ? TimeBoundStatus.PartiallyCompleted : TimeBoundStatus.Completed;

        await using CancellationTokenRegistration registration = RegisterSetAbortingStatus(cancellationToken, timeBound, phaseContext);

        try
        {
            await runAsync(cancellationToken);

            registration.Unregister();
            timeBound.Status = completedStatus;
        }
        catch (Exception exception)
        {
            registration.Unregister();
            timeBound.Status = exception is OperationCanceledException ? TimeBoundStatus.Aborted : completedStatus;
            throw;
        }
        finally
        {
            DateTime finishedAt = ambientService.UtcNow;
            if (!isPartial && timeBound is ITimeBoundWithAfter timeBoundWithAfter)
            {
                timeBoundWithAfter.AfterFinishedAt = finishedAt;
            }
            else
            {
                timeBound.FinishedAt = finishedAt;
            }
        }
    }

    private CancellationTokenRegistration RegisterSetAbortingStatus(
        CancellationToken cancellationToken, ITimeBound timeBound, IPhaseContext phaseContext
    )
    {
        return cancellationToken.Register(
            () =>
            {
                timeBound.Status = TimeBoundStatus.RunningAborting;
                phaseContext.Accept(migrationInfoRepository.Upserter, default).GetAwaiter().GetResult();
            }
        );
    }

    private async Task WithStepHistoryAsync(
        Func<CancellationToken, Task> runAsync,
        IPhaseContext phaseContext,
        IMigratorStepExecutor stepExecutor,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    )
    {
        StepHistory stepHistory = phaseContext.GetStepHistory(stepExecutor.Name);
        stepHistory.Status = TimeBoundStatus.Running;

        DateTime startedAt = ambientService.UtcNow;
        if (stepHistory.StartedAt is null)
        {
            stepHistory.StartedAt = startedAt;
        }
        else
        {
            stepHistory.AfterStartedAt = startedAt;
        }

        await phaseContext.Accept(migrationInfoRepository.Upserter, default);
        await phaseContext.Accept(stepStartedEventEmitter, (eventRecipients, stepHistory));

        try
        {
            using IDisposable? timer = stepExecutor.DisableProgressFlushTimer
                ? null
                : phaseContext.Accept(migrationInfoRepository.TimedProgressFlushStarter, default);
            await WithTimeBoundAsync(runAsync, stepHistory, phaseContext, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            phaseContext.Exception = exception;
        }
        finally
        {
            await phaseContext.Accept(migrationInfoRepository.Upserter, default);
            await phaseContext.Accept(stepFinishedEventEmitter, (eventRecipients, stepHistory));
        }
    }

    private abstract class StepEventEmitter : IPhaseContextVisitor<Task, (IEnumerable<string> Recipients, StepHistory StepHistory)>
    {
        // ReSharper disable once AsyncApostle.AsyncMethodNamingHighlighting
        public Task Visit(IMigrationContext migrationContext, (IEnumerable<string> Recipients, StepHistory StepHistory) arg)
        {
            return EmitAsync(migrationContext, null, arg.Recipients, arg.StepHistory, migrationContext.IsFailed);
        }

        // ReSharper disable once AsyncApostle.AsyncMethodNamingHighlighting
        public Task Visit(ISiteContext siteContext, (IEnumerable<string> Recipients, StepHistory StepHistory) arg)
        {
            return EmitAsync(siteContext.GlobalContext, siteContext.SiteId, arg.Recipients, arg.StepHistory, siteContext.IsFailed);
        }

        protected abstract Task EmitAsync(
            IMigrationContext migrationContext, Guid? siteId, IEnumerable<string> eventRecipients, StepHistory stepHistory, bool isFailed
        );
    }

    private sealed class StepStartedEventEmitter : StepEventEmitter
    {
        private readonly IEventService eventService;

        public StepStartedEventEmitter(IEventService eventService)
        {
            this.eventService = eventService;
        }

        protected override Task EmitAsync(
            IMigrationContext migrationContext, Guid? siteId, IEnumerable<string> eventRecipients, StepHistory stepHistory, bool isFailed
        )
        {
            return eventService.EmitAsync(
                eventRecipients,
                (m, _) =>
                {
                    bool isAfter = stepHistory.AfterStartedAt is not null;
                    return new StepStartedEvent()
                    {
                        InstanceId = migrationContext.InstanceId,
                        Timestamp = (isAfter ? stepHistory.AfterStartedAt : stepHistory.StartedAt)!.Value,
                        Metadata = m,
                        Name = stepHistory.Name,
                        SiteId = siteId,
                        IsAfter = isAfter,
                    };
                }
            );
        }
    }

    private sealed class StepFinishedEventEmitter : StepEventEmitter
    {
        private readonly IEventService eventService;

        public StepFinishedEventEmitter(IEventService eventService)
        {
            this.eventService = eventService;
        }

        protected override Task EmitAsync(
            IMigrationContext migrationContext, Guid? siteId, IEnumerable<string> eventRecipients, StepHistory stepHistory, bool isFailed
        )
        {
            return eventService.EmitAsync(
                eventRecipients,
                (m, _) =>
                {
                    bool isAfter = stepHistory.AfterFinishedAt is not null;
                    return new StepFinishedEvent()
                    {
                        InstanceId = migrationContext.InstanceId,
                        Timestamp = (isAfter ? stepHistory.AfterFinishedAt : stepHistory.FinishedAt)!.Value,
                        Metadata = m,
                        Name = stepHistory.Name,
                        SiteId = siteId,
                        IsAfter = isAfter,
                        Status = isFailed ? FinishedEventStatus.Failed
                            : stepHistory.Status == TimeBoundStatus.Aborted ? FinishedEventStatus.Aborted
                            : FinishedEventStatus.Completed,
                    };
                }
            );
        }
    }

#pragma warning disable SA1204
    private static partial class LogMessages
#pragma warning restore SA1204
    {
        [LoggerMessage(0, LogLevel.Information, "Running global migrator step")]
        internal static partial void RunningGlobalMigratorStep(ILogger logger);

        [LoggerMessage(1, LogLevel.Information, "Running site migrator step")]
        internal static partial void RunningSiteMigratorStep(ILogger logger);

        [LoggerMessage(2, LogLevel.Information, "Running global migrator step, after phase")]
        internal static partial void RunningGlobalMigratorStepAfter(ILogger logger);

        [LoggerMessage(3, LogLevel.Information, "Running site migrator step, after phase")]
        internal static partial void RunningSiteMigratorStepAfter(ILogger logger);
    }
}
