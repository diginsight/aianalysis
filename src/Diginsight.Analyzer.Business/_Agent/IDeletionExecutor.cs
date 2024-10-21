namespace Diginsight.Analyzer.Business;

internal interface IDeletionExecutor
{
    Task ExecuteAsync(
        Guid instanceId,
        IEnumerable<Guid> siteIds,
        DeletionMode mode,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    );
}
