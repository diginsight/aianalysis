namespace Diginsight.Analyzer.Entities.Events;

public sealed class AnalysisFinishedEvent : Event, IFinishedEvent
{
    public override EventKind EventKind => EventKind.AnalysisFinished;

    public required FinishedEventStatus Status { get; init; }
}
