namespace Diginsight.Analyzer.Business.Models;

[Serialized]
internal sealed class StartDeletionRequestBody
{
    public required IEnumerable<Guid> SiteIds { get; init; }

    public required DeletionMode? Mode { get; init; }
}
