namespace Diginsight.Analyzer.Business;

public interface IAgentMigrationContextFactory
{
    IMigrationContext Make(
        Guid instanceId,
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<string> globalStepNames,
        IEnumerable<string> siteStepNames,
        DateTime? queuedAt
    );
}
