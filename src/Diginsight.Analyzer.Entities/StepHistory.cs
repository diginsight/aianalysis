using Diginsight.Analyzer.Common.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

[Serialized]
public sealed class StepHistory : ITimeBoundWithTeardown
{
    [JsonConstructor]
    public StepHistory(string name)
    {
        Name = name;
    }

    public string Name { get; }

    [DisallowNull]
    public DateTime? StartedAt { get; set; }

    [DisallowNull]
    public DateTime? FinishedAt { get; set; }

    [DisallowNull]
    public DateTime? TeardownStartedAt { get; set; }

    [DisallowNull]
    public DateTime? TeardownFinishedAt { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public TimeBoundStatus Status { get; set; }
}
