namespace Diginsight.Analyzer.Entities;

public interface ISkippable : IAnalyzed
{
    bool IsSkipped { get; }

    string? Reason { get; }

    void Skip(string reason);
}
