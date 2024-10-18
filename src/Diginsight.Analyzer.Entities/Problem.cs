using Diginsight.Analyzer.Common.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Entities;

[Serialized]
public sealed class Problem
{
    private Problem(ProblemKind kind, string? reason, Exception? exception)
    {
        Kind = kind;
        Reason = reason;
        Exception = exception;
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public ProblemKind Kind { get; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string? Reason { get; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public Exception? Exception { get; }

    public static Problem Failed(Exception exception) => new (ProblemKind.Failed, null, exception);

    public static Problem Failed(string? reason = null) => new (ProblemKind.Failed, reason, null);

    public static Problem Skipped(string? reason = null) => new (ProblemKind.Skipped, reason, null);
}
