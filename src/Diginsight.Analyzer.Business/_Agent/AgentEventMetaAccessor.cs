namespace Diginsight.Analyzer.Business;

internal sealed class AgentEventMetaAccessor : EventMetaAccessor, IAgentEventMetaAccessor
{
    public AgentEventMetaAccessor(IHttpContextAccessor httpContextAccessor, IServiceProvider serviceProvider)
        : base(httpContextAccessor, serviceProvider) { }

    public void Set(IReadOnlyDictionary<string, IEnumerable<string>> eventMeta)
    {
        eventMetaLocal.Value = eventMeta;
    }
}
