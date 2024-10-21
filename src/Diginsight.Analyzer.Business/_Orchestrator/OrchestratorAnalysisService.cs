using Diginsight.Analyzer.Business.Models;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed class OrchestratorAnalysisService : IOrchestratorAnalysisService
{
    private readonly IOrchestratorExecutionService executionService;
    private readonly IInternalMigrationService internalMigrationService;
    private readonly IOrchestratorMigrationContextFactory migrationContextFactory;
    private readonly IMigrationInfoRepository migrationInfoRepository;
    private readonly IAmbientService ambientService;
    private readonly IOptionsMonitor<CoreConfig> coreConfigMonitor;

    public OrchestratorAnalysisService(
        IOrchestratorExecutionService executionService,
        IInternalMigrationService internalMigrationService,
        IOrchestratorMigrationContextFactory migrationContextFactory,
        IMigrationInfoRepository migrationInfoRepository,
        IAmbientService ambientService,
        IOptionsMonitor<CoreConfig> coreConfigMonitor
    )
    {
        this.executionService = executionService;
        this.internalMigrationService = internalMigrationService;
        this.migrationContextFactory = migrationContextFactory;
        this.migrationInfoRepository = migrationInfoRepository;
        this.ambientService = ambientService;
        this.coreConfigMonitor = coreConfigMonitor;
    }

    private IOrchestratorCoreConfig CoreConfig => coreConfigMonitor.CurrentValue;

    public async Task<(Guid InstanceId, bool Queued)> StartAsync(
        GlobalInfo globalInfo,
        IDictionary<Guid, SiteInfo> sites,
        IEnumerable<string>? globalStepNames,
        IEnumerable<string>? siteStepNames,
        string? family,
        QueuingPolicy queuingPolicy,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    )
    {
        family ??= CoreConfig.DefaultFamily;

        StrongBox<GlobalInfo> globalInfoBox = new (globalInfo);

        IEnumerable<IMigratorStep> sortedMigratorSteps = await internalMigrationService.CalculateStepsAsync(
            globalInfoBox,
            sites,
            globalStepNames,
            siteStepNames,
            cancellationToken
        );

        globalInfo = globalInfoBox.Value!;
        IReadOnlyDictionary<Guid, SiteInfo> finalSites = new Dictionary<Guid, SiteInfo>(sites);

        Guid instanceId;
        bool queued;
        try
        {
            instanceId = await executionService.StartAsync(
                family,
                ExecutionKind.Migration,
                (lease, ct) => internalMigrationService.HasConflictAsync(globalInfo, finalSites, lease, sortedMigratorSteps, ct),
                ac => ac.StartMigrationAsync(
                    new StartMigrationRequestBody()
                    {
                        GlobalInfo = globalInfo,
                        Sites = finalSites,
                        GlobalSteps = globalStepNames,
                        SiteSteps = siteStepNames,
                    },
                    eventRecipients
                ),
                cancellationToken
            );

            queued = false;
        }
        catch (MigrationException exception) when (ShouldQueue(exception))
        {
            instanceId = await QueueAsync(family, globalInfo, finalSites, sortedMigratorSteps, eventRecipients);
            queued = true;
        }

        bool ShouldQueue(MigrationException me)
        {
            return (me.Label == IOrchestratorExecutionService.NoAgentAvailableExceptionLabel &&
                    (queuingPolicy & QueuingPolicy.IfFull) != 0) ||
                (me.Label == nameof(MigrationExceptions.ConflictingExecution) &&
                    (queuingPolicy & QueuingPolicy.IfConflict) != 0);
        }

        return (instanceId, queued);
    }

    public async Task CancelAsync(Guid instanceId)
    {
        if (await migrationInfoRepository.GetMigrationSnapshotAsync(instanceId, false) is not { } snapshot)
        {
            throw MigrationExceptions.NoSuchInstance;
        }

        if (snapshot.Status != TimeBoundStatus.Pending)
        {
            throw MigrationExceptions.NotPending;
        }

        await migrationInfoRepository.DeleteAsync(snapshot.InstanceId, snapshot.Id);
    }

    public Task<IEnumerable<Guid>> AbortAsync(Guid? instanceId)
    {
        return executionService.AbortAsync(ExecutionKind.Migration, instanceId);
    }

    private async Task<Guid> QueueAsync(
        string family,
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<IMigratorStep> sortedMigratorSteps,
        IEnumerable<string> eventRecipients
    )
    {
        Guid instanceId = ambientService.NewGuid();
        IMigrationContext migrationContext = migrationContextFactory.Make(
            instanceId,
            family,
            globalInfo,
            sites,
            sortedMigratorSteps.OfType<IGlobalMigratorStep>().Select(static x => x.Name),
            sortedMigratorSteps.OfType<ISiteMigratorStep>().Select(static x => x.Name),
            eventRecipients
        );

        await migrationInfoRepository.InsertAsync(migrationContext);

        return instanceId;
    }
}
