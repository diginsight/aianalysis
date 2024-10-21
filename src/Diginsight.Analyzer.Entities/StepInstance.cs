using Diginsight.Analyzer.Common.Annotations;
using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

[Serialized]
public class StepInstance
{
    [JsonConstructor]
    public StepInstance(string template, string internalName, string displayName, StepInput input)
    {
        Template = template;
        InternalName = internalName;
        DisplayName = displayName;
        Input = input;
    }

    public string Template { get; }

    public string InternalName { get; }

    public string DisplayName { get; }

    public StepInput Input { get; }
}
