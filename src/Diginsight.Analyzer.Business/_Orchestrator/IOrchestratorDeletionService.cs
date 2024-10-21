namespace Diginsight.Analyzer.Business;

public interface IOrchestratorDeletionService : IDeletionService
{
    Task<Guid> StartAsync(
        IEnumerable<Guid> siteIds,
        DeletionMode mode,
        string? family,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    );
}
