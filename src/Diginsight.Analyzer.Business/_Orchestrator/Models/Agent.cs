namespace Diginsight.Analyzer.Business.Models;

internal class Agent
{
    public required Uri BaseAddress { get; init; }

    public required string MachineName { get; init; }

    public required string Family { get; init; }
}
