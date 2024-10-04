using JetBrains.Annotations;

namespace Diginsight.AIAnalysis;

public interface IAnalysisService
{
    void LabelAnalysis(DateTime? maybeTimestamp, out DateTime timestamp, out Guid analysisId);

    Task AnalyzeAsync(
        DateTime timestamp,
        string logContent,
        TextWriter summaryWriter,
        IReadOnlyDictionary<string, object?> placeholderDict,
        CancellationToken cancellationToken = default
    );

    Task WriteLogAsync(DateTime timestamp, Guid analysisId, Stream stream, CancellationToken cancellationToken = default);

    Task ConsolidateAsync(Guid analysisId, string title, CancellationToken cancellationToken = default);

    Task<Stream?> TryGetLogStreamAsync(Guid analysisId, CancellationToken cancellationToken = default);

    Task<Stream?> TryGetSummaryStreamAsync(Guid analysisId, CancellationToken cancellationToken = default);
}
