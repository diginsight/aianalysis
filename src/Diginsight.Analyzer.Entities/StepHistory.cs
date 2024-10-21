using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public sealed class StepHistory : StepInstance, ITimeBoundWithTeardown
{
    [JsonConstructor]
    public StepHistory(string template, string internalName, string displayName, StepInput input)
        : base(template, internalName, displayName, input) { }

    [DisallowNull]
    public DateTime? StartedAt { get; set; }

    [DisallowNull]
    public DateTime? FinishedAt { get; set; }

    [DisallowNull]
    public DateTime? TeardownStartedAt { get; set; }

    [DisallowNull]
    public DateTime? TeardownFinishedAt { get; set; }

    public TimeBoundStatus Status { get; set; }
}
