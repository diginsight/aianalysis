using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public interface IFailable : IAnalyzed
{
    bool IsFailed { get; }

    [DisallowNull]
    [JsonProperty(TypeNameHandling = TypeNameHandling.Auto)]
    Exception? Exception { get; set; }

    string? Reason { get; }

    void Fail(string reason);
}
