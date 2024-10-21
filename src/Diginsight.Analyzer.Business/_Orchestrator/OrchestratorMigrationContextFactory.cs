using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

internal sealed class OrchestratorMigrationContextFactory : IOrchestratorMigrationContextFactory
{
    private readonly IAmbientService ambientService;
    private readonly IParallelismSettingsAccessor parallelismSettingsAccessor;
    private readonly IEventMetaAccessor eventMetaAccessor;

    public OrchestratorMigrationContextFactory(
        IAmbientService ambientService,
        IParallelismSettingsAccessor parallelismSettingsAccessor,
        IEventMetaAccessor eventMetaAccessor
    )
    {
        this.ambientService = ambientService;
        this.parallelismSettingsAccessor = parallelismSettingsAccessor;
        this.eventMetaAccessor = eventMetaAccessor;
    }

    public IMigrationContext Make(
        Guid instanceId,
        string family,
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<string> globalStepNames,
        IEnumerable<string> siteStepNames,
        IEnumerable<string> eventRecipients
    )
    {
        DequeuingInfo dequeuingInfo = new ()
        {
            Parallelism = parallelismSettingsAccessor.Get().ToDictionary(static x => string.Join('-', x.Key), static x => x.Value),
            EventRecipients = eventRecipients,
            EventMeta = eventMetaAccessor.Get(),
        };

        return new MigrationContext(
            instanceId,
            globalInfo,
            sites,
            globalStepNames,
            siteStepNames,
            ambientService.UtcNow,
            family,
            dequeuingInfo: dequeuingInfo
        );
    }
}
