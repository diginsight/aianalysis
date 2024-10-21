using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentAnalysisService : IAgentAnalysisService
{
    private static readonly MigrationException NoSuchInstanceException = new ("No such instance", HttpStatusCode.NotFound, "NoSuchInstance");

    private readonly IAgentExecutionService executionService;
    private readonly IInternalMigrationService internalMigrationService;
    private readonly ISnapshotService snapshotService;
    private readonly IAgentParallelismSettingsAccessor parallelismSettingsAccessor;
    private readonly IAgentEventMetaAccessor eventMetaAccessor;

    public AgentAnalysisService(
        IAgentExecutionService executionService,
        IInternalMigrationService internalMigrationService,
        ISnapshotService snapshotService,
        IAgentParallelismSettingsAccessor parallelismSettingsAccessor,
        IAgentEventMetaAccessor eventMetaAccessor
    )
    {
        this.executionService = executionService;
        this.internalMigrationService = internalMigrationService;
        this.snapshotService = snapshotService;
        this.parallelismSettingsAccessor = parallelismSettingsAccessor;
        this.eventMetaAccessor = eventMetaAccessor;
    }

    public async Task<Guid> StartAsync(
        GlobalInfo globalInfo,
        IDictionary<Guid, SiteInfo> sites,
        IEnumerable<string>? globalStepNames,
        IEnumerable<string>? siteStepNames,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    )
    {
        StrongBox<GlobalInfo> globalInfoBox = new (globalInfo);

        IEnumerable<IMigratorStep> sortedMigratorSteps = await internalMigrationService.CalculateStepsAsync(
            globalInfoBox, sites, globalStepNames, siteStepNames, cancellationToken
        );

        globalInfo = globalInfoBox.Value!;
        IReadOnlyDictionary<Guid, SiteInfo> finalSites = new Dictionary<Guid, SiteInfo>(sites);

        return await StartAsync(globalInfo, finalSites, sortedMigratorSteps, null, null, eventRecipients, cancellationToken);
    }

    public async Task DequeueAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        if (await snapshotService.GetMigrationAsync(instanceId, false) is not { } migrationSnapshot)
        {
            throw NoSuchInstanceException;
        }

        if (migrationSnapshot.StartedAt is not null)
        {
            throw MigrationExceptions.NotPending;
        }

        IEnumerable<SiteContextSnapshot> siteSnapshots = (await snapshotService.GetSitesAsync(instanceId, false))!;

        StrongBox<GlobalInfo> globalInfoBox = new (migrationSnapshot.GlobalInfo);
        IDictionary<Guid, SiteInfo> sites = siteSnapshots.ToDictionary(static x => x.SiteId, static x => x.SiteInfo);
        IEnumerable<string> globalStepNames = migrationSnapshot.StepHistories.Select(static x => x.Name);
        IEnumerable<string> siteStepNames = siteSnapshots.FirstOrDefault()?.StepHistories.Select(static x => x.Name) ?? Enumerable.Empty<string>();

        IEnumerable<IMigratorStep> sortedMigratorSteps = await internalMigrationService.CalculateStepsAsync(
            globalInfoBox, sites, globalStepNames, siteStepNames, cancellationToken
        );

        GlobalInfo globalInfo = globalInfoBox.Value!;
        IReadOnlyDictionary<Guid, SiteInfo> finalSites = new Dictionary<Guid, SiteInfo>(sites);

        DequeuingInfo dequeuingInfo = migrationSnapshot.DequeuingInfo!;

        parallelismSettingsAccessor.Set(dequeuingInfo.Parallelism);
        eventMetaAccessor.Set(dequeuingInfo.EventMeta.ToDictionary(static x => x.Key, static x => x.Value.ToArray().AsEnumerable()));

        _ = await StartAsync(
            globalInfo,
            finalSites,
            sortedMigratorSteps,
            instanceId,
            migrationSnapshot.QueuedAt,
            dequeuingInfo.EventRecipients,
            cancellationToken
        );
    }

    public Task<(ExecutionKind Kind, Guid InstanceId)?> GetCurrentAsync() => executionService.GetCurrentAsync();

    public Task<IEnumerable<Guid>> AbortAsync(Guid? instanceId)
    {
        return executionService.AbortAsync(ExecutionKind.Migration, instanceId);
    }

    private Task<Guid> StartAsync(
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<IMigratorStep> sortedMigratorSteps,
        Guid? requestedInstanceId,
        DateTime? queuedAt,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    )
    {
        return executionService.StartAsync<MigrationLease>(
            ExecutionKind.Migration,
            requestedInstanceId,
            sites.Keys,
            lease => internalMigrationService.FillLease(lease, sortedMigratorSteps),
            (lease, ct) => internalMigrationService.HasConflictAsync(globalInfo, sites, lease, sortedMigratorSteps, ct),
            (instanceId, ct) => CoreStartAsync(instanceId, queuedAt, globalInfo, sites, sortedMigratorSteps, eventRecipients, ct),
            cancellationToken
        );
    }

    private Task CoreStartAsync(
        Guid instanceId,
        DateTime? queuedAt,
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<IMigratorStep> sortedMigratorSteps,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    )
    {
        return executionService.RunDetachedAsync(
            sp =>
            {
                IEnumerable<IGlobalMigratorStepExecutor> globalMigratorStepExecutors = sortedMigratorSteps
                    .OfType<IGlobalMigratorStep>()
                    .Select(x => x.CreateExecutor(sp))
                    .ToArray();
                IEnumerable<ISiteMigratorStepExecutor> siteMigratorStepExecutors = sortedMigratorSteps
                    .OfType<ISiteMigratorStep>()
                    .Select(x => x.CreateExecutor(sp))
                    .ToArray();

                return (globalMigratorStepExecutors, siteMigratorStepExecutors, sp.GetRequiredService<IMigrationExecutor>());
            },
            (services, ct) =>
            {
                var (globalMigratorStepExecutors, siteMigratorStepExecutors, migrationExecutor) = services;

                return migrationExecutor
                    .ExecuteAsync(
                        instanceId,
                        queuedAt,
                        globalInfo,
                        sites,
                        globalMigratorStepExecutors,
                        siteMigratorStepExecutors,
                        eventRecipients,
                        ct
                    );
            },
            cancellationToken
        );
    }
}
