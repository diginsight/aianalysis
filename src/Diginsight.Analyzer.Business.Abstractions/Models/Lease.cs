using Diginsight.Analyzer.Entities;
using Newtonsoft.Json;

namespace Diginsight.Analyzer.Business.Models;

public class Lease : Expandable<Lease>
{
    private string? id;

    [JsonConstructor]
    public Lease() { }

    public Lease(Lease other)
    {
        Id = other.Id;
        BaseAddress = other.BaseAddress;
        AgentName = other.AgentName;
        AgentPool = other.AgentPool;
        TtlSeconds = other.TtlSeconds;
    }

    [JsonProperty("id")]
    public string Id
    {
        get => id ??= Guid.NewGuid().ToString();
        private init => id = value;
    }

    public Uri BaseAddress { get; init; } = null!;

    public string AgentName { get; init; } = null!;

    public string AgentPool { get; init; } = null!;

    [JsonProperty("ttl")]
    public int TtlSeconds { get; init; }

    [JsonProperty(NullValueHandling = NullValueHandling.Include)]
    public ExecutionKind? Kind { get; set; }

    public virtual ActiveLease? AsActive() => Kind switch
    {
        ExecutionKind.Analysis => As<AnalysisLease>(),
        _ => null,
    };
}
