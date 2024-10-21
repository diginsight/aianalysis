namespace Diginsight.Analyzer.Business.Configurations;

public interface IOrchestratorCoreConfig : ICoreConfig
{
    int AgentTimeoutSeconds { get; }

    int DequeuerIntervalSeconds { get; }

    int DequeuerMaxFailures { get; }

    bool AllowAllEventsNotification { get; }
}
