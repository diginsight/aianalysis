namespace Diginsight.AIAnalysis.API.Services;

internal sealed class InnerAnalysisService : IInnerAnalysisService
{
    private readonly IAnalysisService analysisService;

    public InnerAnalysisService(IAnalysisService analysisService)
    {
        this.analysisService = analysisService;
    }

    public async Task AnalyzeAsync(DateTime timestamp, Guid analysisId, string logContent, IReadOnlyDictionary<string, object?> placeholderDict)
    {
        throw new NotImplementedException();
    }
}
