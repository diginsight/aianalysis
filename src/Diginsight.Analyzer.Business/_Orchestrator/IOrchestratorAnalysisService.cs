namespace Diginsight.Analyzer.Business;

public interface IOrchestratorAnalysisService : IAnalysisService
{
    Task<(Guid InstanceId, bool Queued)> StartAsync(
        GlobalInfo globalInfo,
        IDictionary<Guid, SiteInfo> sites,
        IEnumerable<string>? globalStepNames,
        IEnumerable<string>? siteStepNames,
        string? family,
        QueuingPolicy queuingPolicy,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    );

    Task CancelAsync(Guid instanceId);
}
