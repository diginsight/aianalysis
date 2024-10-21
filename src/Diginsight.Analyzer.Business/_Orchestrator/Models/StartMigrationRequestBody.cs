namespace Diginsight.Analyzer.Business.Models;

[Serialized]
internal sealed class StartMigrationRequestBody
{
    public required GlobalInfo GlobalInfo { get; init; }

    public required IReadOnlyDictionary<Guid, SiteInfo> Sites { get; init; }

    public required IEnumerable<string>? GlobalSteps { get; init; }

    public required IEnumerable<string>? SiteSteps { get; init; }
}
