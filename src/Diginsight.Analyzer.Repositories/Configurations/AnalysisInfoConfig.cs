using Diginsight.Analyzer.Common.Annotations;

namespace Diginsight.Analyzer.Repositories.Configurations;

[Configuration]
internal class AnalysisInfoConfig : IAnalysisInfoConfig
{
    public int TimedProgressFlushSeconds { get; set; } = 30;
}
