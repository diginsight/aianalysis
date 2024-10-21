namespace Diginsight.Analyzer.Business.Models;

internal sealed class ActiveAgent : Agent
{
    public required ExecutionKind Kind { get; init; }

    public required Guid InstanceId { get; init; }

    public required bool IsConflicting { get; init; }
}
