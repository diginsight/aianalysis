namespace Diginsight.Analyzer.Business;

internal interface IAgentEventMetaAccessor : IEventMetaAccessor
{
    void Set(IReadOnlyDictionary<string, IEnumerable<string>> eventMeta);
}
