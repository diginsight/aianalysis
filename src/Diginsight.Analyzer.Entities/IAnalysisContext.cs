namespace Diginsight.Analyzer.Entities;

public interface IAnalysisContext : IExecutionContext, ITimeBound
{
    AnalysisCoord AnalysisCoord { get; }

    string AgentName { get; }

    string AgentPool { get; }

    DateTime? QueuedAt { get; }

    DateTime StartedAt { get; }

    GlobalInput GlobalInput { get; }

    IEnumerable<StepHistory> Steps { get; }

    StepHistory GetStep(string internalName);

    T GetProgress<T>()
        where T : Progress, new();
}
