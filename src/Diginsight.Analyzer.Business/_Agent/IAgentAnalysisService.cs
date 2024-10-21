namespace Diginsight.Analyzer.Business;

public interface IAgentAnalysisService : IAnalysisService, IAsyncDisposable
{
    Task<Guid> StartAsync(
        GlobalInfo globalInfo,
        IDictionary<Guid, SiteInfo> sites,
        IEnumerable<string>? globalStepNames,
        IEnumerable<string>? siteStepNames,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    );

    Task DequeueAsync(Guid instanceId, CancellationToken cancellationToken);

    Task<(ExecutionKind Kind, Guid InstanceId)?> GetCurrentAsync();

    ValueTask IAsyncDisposable.DisposeAsync()
    {
        return new ValueTask(AbortAsync(null));
    }
}
