namespace Diginsight.Analyzer.Entities;

public interface IAnalysisContext : ITimeBound, IFailable
{
    Guid PersistenceId { get; }

    AnalysisCoordinate Coordinate { get; }

    DateTime? QueuedAt { get; }

    DateTime StartedAt { get; }

    AnalysisInfo AnalysisInfo { get; }

    IEnumerable<StepHistory> StepHistories { get; }

    T GetProgress<T>()
        where T : AnalysisProgress, new();

    StepHistory GetStepHistory(string stepName);

    bool IsNotStarted();
}
