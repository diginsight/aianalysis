namespace Diginsight.Analyzer.Entities.Events;

public abstract class StepEvent : Event
{
    public required string Template { get; init; }

    public required string InternalName { get; init; }

    public required bool IsTeardown { get; init; }
}
