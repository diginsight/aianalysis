using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

internal interface IOrchestratorLeaseService
{
    IAsyncEnumerable<Agent> GetAgentsAE(
        string? family,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        CancellationToken cancellationToken
    );

    IAsyncEnumerable<Agent> GetAllAgentsAE(CancellationToken cancellationToken);
}
