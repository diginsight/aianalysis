namespace Diginsight.AIAnalysis.API.Services;

internal interface IInnerAnalysisService
{
    Task AnalyzeAsync(
        DateTime timestamp,
        Guid analysisId,
        string logContent,
        IReadOnlyDictionary<string, object?> placeholderDict
    );
}
