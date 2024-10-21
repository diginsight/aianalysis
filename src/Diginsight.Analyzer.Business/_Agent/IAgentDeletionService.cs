namespace Diginsight.Analyzer.Business;

public interface IAgentDeletionService : IDeletionService, IAsyncDisposable
{
    Task<Guid> StartAsync(IEnumerable<Guid> siteIds, DeletionMode mode, IEnumerable<string> eventRecipients, CancellationToken cancellationToken);

    Task<(ExecutionKind Kind, Guid InstanceId)?> GetCurrentAsync();

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return new ValueTask(AbortAsync(null));
    }
}
