namespace Diginsight.Analyzer.Business.Configurations;

public interface ICoreConfig
{
    int DefaultParallelism { get; }

    int DefaultPageSize { get; }

    int MaxPageSize { get; }

    string DefaultAgentPool { get; }
}
