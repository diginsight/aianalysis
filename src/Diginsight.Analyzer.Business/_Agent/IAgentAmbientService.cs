namespace Diginsight.Analyzer.Business;

public interface IAgentAmbientService : IAmbientService
{
    Uri BaseAddress { get; }

    string MachineName { get; }

    string Family { get; }
}
