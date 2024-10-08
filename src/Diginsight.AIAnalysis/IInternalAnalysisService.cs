using System.Text;

namespace Diginsight.AIAnalysis;

internal interface IInternalAnalysisService
{
    void LabelAnalysis(DateTime? maybeTimestamp, out DateTime timestamp, out Guid analysisId);

    Task WriteLogAsync(DateTime timestamp, Guid analysisId, Stream stream, CancellationToken cancellationToken = default);

    Task<string> AnalyzeAsync(
        DateTime timestamp,
        Guid analysisId,
        string logContent,
        IReadOnlyDictionary<string, object?> placeholderDict,
        CancellationToken cancellationToken = default
    );

    Task ConsolidateAsync(Guid analysisId, string title, CancellationToken cancellationToken = default);

    Task<string?> TryGetTitleAsync(Guid analysisId, CancellationToken cancellationToken = default);

    Task<(Stream Stream, Encoding Encoding)?> TryGetLogAsync(Guid analysisId, CancellationToken cancellationToken = default);

    Task<(Stream Stream, Encoding Encoding)?> TryGetSummaryAsync(Guid analysisId, CancellationToken cancellationToken = default);
}
