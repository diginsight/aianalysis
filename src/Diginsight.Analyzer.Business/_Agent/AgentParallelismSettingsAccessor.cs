using Microsoft.Extensions.Options;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentParallelismSettingsAccessor : ParallelismSettingsAccessor, IAgentParallelismSettingsAccessor
{
    private readonly IOptionsMonitor<CoreConfig> coreConfigMonitor;

    public AgentParallelismSettingsAccessor(
        IHttpContextAccessor httpContextAccessor,
        IServiceProvider serviceProvider,
        IOptionsMonitor<CoreConfig> coreConfigMonitor
    )
        : base(httpContextAccessor, serviceProvider)
    {
        this.coreConfigMonitor = coreConfigMonitor;
    }

    private ICoreConfig CoreConfig => coreConfigMonitor.CurrentValue;

    public void Set(IParallelismSettings parallelismSettings)
    {
        parallelismSettingsLocal.Value = parallelismSettings;
    }

    public void Set(IReadOnlyDictionary<string, int> parallelismSettings)
    {
        Set(new DetachedParallelismSettings(CoreConfig.DefaultParallelism, parallelismSettings));
    }
}
