namespace Diginsight.Analyzer.Entities.Events;

public abstract class StepEvent : Event
{
    public required string Name { get; init; }

    public required bool IsTeardown { get; init; }
}
