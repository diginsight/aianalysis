namespace Diginsight.Analyzer.Entities.Events;

public sealed class AnalysisStartedEvent : Event, IStartedEvent
{
    public override EventKind EventKind => EventKind.AnalysisStarted;

    public required bool Queued { get; init; }
}
