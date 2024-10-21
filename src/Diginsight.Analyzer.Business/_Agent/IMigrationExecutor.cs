namespace Diginsight.Analyzer.Business;

internal interface IMigrationExecutor
{
    Task ExecuteAsync(
        Guid instanceId,
        DateTime? queuedAt,
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<IGlobalMigratorStepExecutor> globalMigratorStepExecutors,
        IEnumerable<ISiteMigratorStepExecutor> siteMigratorStepExecutors,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    );
}
