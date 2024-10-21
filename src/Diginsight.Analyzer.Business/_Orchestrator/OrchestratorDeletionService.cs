using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

internal sealed class OrchestratorDeletionService : IOrchestratorDeletionService
{
    private readonly IOrchestratorExecutionService executionService;

    public OrchestratorDeletionService(
        IOrchestratorExecutionService executionService
    )
    {
        this.executionService = executionService;
    }

    public Task<Guid> StartAsync(
        IEnumerable<Guid> siteIds,
        DeletionMode mode,
        string? family,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    )
    {
        return executionService.StartAsync(
            family,
            ExecutionKind.Deletion,
            (lease, _) => Task.FromResult(siteIds.Intersect(lease.SiteIds).Any()),
            ac => ac.StartDeletionAsync(
                new StartDeletionRequestBody()
                {
                    SiteIds = siteIds,
                    Mode = mode,
                },
                eventRecipients
            ),
            cancellationToken
        );
    }

    public Task<IEnumerable<Guid>> AbortAsync(Guid? instanceId)
    {
        return executionService.AbortAsync(ExecutionKind.Deletion, instanceId);
    }
}
