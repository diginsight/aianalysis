using Diginsight.Analyzer.Entities;
using Newtonsoft.Json;

namespace Diginsight.Analyzer.Repositories.Models;

public abstract class ExecutionContextSnapshot
{
    private readonly ExecutionKind executionKind;

    protected ExecutionContextSnapshot(Guid executionId)
    {
        ExecutionId = executionId;
    }

    [JsonProperty("id")]
    public string Id => ExecutionId.ToString("D");

    public ExecutionKind ExecutionKind
    {
        get => executionKind;
        init
        {
            executionKind = value;
            ExecutionCoord = new ExecutionCoord(value, ExecutionId);
        }
    }

    [JsonIgnore]
    public Guid ExecutionId { get; }

    [JsonIgnore]
    public ExecutionCoord ExecutionCoord { get; private init; }

    public bool IsFailed { get; init; }

    [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
    public Exception? Exception { get; init; }

    public string? Reason { get; init; }
}
