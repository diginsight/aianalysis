namespace Diginsight.Analyzer.Business;

public interface IOrchestratorMigrationContextFactory
{
    IMigrationContext Make(
        Guid instanceId,
        string family,
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<string> globalStepNames,
        IEnumerable<string> siteStepNames,
        IEnumerable<string> eventRecipients
    );
}
