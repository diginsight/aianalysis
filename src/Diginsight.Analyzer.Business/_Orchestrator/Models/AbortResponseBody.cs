namespace Diginsight.Analyzer.Business.Models;

[Deserialized]
internal sealed class AbortResponseBody
{
    public IEnumerable<Guid> InstanceIds { get; init; } = null!;
}
