namespace Diginsight.Analyzer.Entities;

public sealed class DequeuingInfo
{
    public required IEnumerable<string> EventRecipients { get; init; }

    public required IReadOnlyDictionary<string, IEnumerable<string>> EventMeta { get; init; }
}
