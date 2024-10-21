using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

internal interface IOrchestratorExecutionService : IExecutionService
{
    public const string NoAgentAvailableExceptionLabel = "NoAgentAvailable";

    Task<Guid> StartAsync(
        string? family,
        ExecutionKind kind,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        Func<IAgentClient, Task<StartResponseBody>> coreStartAsync,
        CancellationToken cancellationToken
    );

    Task<bool> DequeueAsync(Guid instanceId, string family, CancellationToken cancellationToken);
}
