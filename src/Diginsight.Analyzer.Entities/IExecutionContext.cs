namespace Diginsight.Analyzer.Entities;

public interface IExecutionContext : IFailable
{
    ExecutionCoord ExecutionCoord { get; }

    bool IsNotStarted();
}
