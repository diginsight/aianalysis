namespace Diginsight.Analyzer.Business.Configurations;

public interface IAgentCoreConfig : ICoreConfig
{
    int LeaseTtlMinutes { get; }
}
