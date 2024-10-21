using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities.Events;

public class StepCustomEvent : StepEvent
{
    [JsonConstructor]
    protected StepCustomEvent() { }

    public override EventKind EventKind => EventKind.StepCustom;
}
