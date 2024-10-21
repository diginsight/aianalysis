namespace Diginsight.Analyzer.Business;

internal interface IAgentParallelismSettingsAccessor : IParallelismSettingsAccessor
{
    void Set(IParallelismSettings parallelismSettings);

    void Set(IReadOnlyDictionary<string, int> parallelismSettings);
}
