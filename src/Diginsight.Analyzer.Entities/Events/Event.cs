using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Diginsight.Analyzer.Entities.Events;

public abstract class Event
{
    [JsonConverter(typeof(StringEnumConverter))]
    public abstract EventKind EventKind { get; }

    public required Guid AnalysisId { get; init; }

    public required int Attempt { get; init; }

    public required DateTime Timestamp { get; init; }

    public required IReadOnlyDictionary<string, IEnumerable<string>> Metadata { get; init; }
}
