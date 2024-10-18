using Diginsight.Analyzer.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Repositories.Models;

public sealed class AnalysisContextSnapshot
{
    [JsonProperty("id")]
    public string Id { get; init; } = null!;

    public AnalysisCoordinate Coordinate { get; }

    public DateTime? QueuedAt { get; init; }

    public DateTime? StartedAt { get; init; }

    public DateTime? FinishedAt { get; init; }

    public bool IsFailed { get; init; }

    [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
    public Exception? Exception { get; init; }

    public string? Reason { get; init; }

    public DequeuingInfo? DequeuingInfo { get; init; }

    public AnalysisInfo AnalysisInfo { get; init; } = null!;

    public IEnumerable<StepHistory> StepHistories { get; init; } = null!;

    [JsonConverter(typeof(StringEnumConverter))]
    public TimeBoundStatus Status { get; init; }

    [DisallowNull]
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public AnalysisProgress? Progress { get; set; }

    [JsonConstructor]
    internal AnalysisContextSnapshot(Guid analysisId, int attempt)
    {
        Coordinate = new AnalysisCoordinate(analysisId, attempt);
    }
}
