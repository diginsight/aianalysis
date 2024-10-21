namespace Diginsight.Analyzer.Business;

internal sealed class AgentClientFactory : IAgentClientFactory
{
    private readonly IHttpClientFactory httpClientFactory;

    public AgentClientFactory(IHttpClientFactory httpClientFactory)
    {
        this.httpClientFactory = httpClientFactory;
    }

    public IAgentClient Make(Uri baseAddress) => new AgentClient(httpClientFactory, baseAddress);
}
