using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentMigrationContextFactory : IAgentMigrationContextFactory
{
    private readonly IAgentAmbientService ambientService;

    public AgentMigrationContextFactory(IAgentAmbientService ambientService)
    {
        this.ambientService = ambientService;
    }

    public IMigrationContext Make(
        Guid instanceId,
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<string> globalStepNames,
        IEnumerable<string> siteStepNames,
        DateTime? queuedAt
    )
    {
        return new MigrationContext(
            instanceId,
            globalInfo,
            sites,
            globalStepNames,
            siteStepNames,
            queuedAt,
            ambientService.Family,
            startedAt: ambientService.UtcNow,
            machineName: ambientService.MachineName
        )
        {
            Status = TimeBoundStatus.Running,
        };
    }
}
