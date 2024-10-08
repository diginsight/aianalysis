using System.Text;

namespace Diginsight.AIAnalysis;

public interface IAnalysisService
{
    Task<IPartialAnalysisResult> StartAnalyzeAsync(
        Stream logStream,
        Encoding logEncoding,
        IReadOnlyDictionary<string, object?> placeholders,
        DateTime? timestamp = null,
        CancellationToken cancellationToken = default
    );

    Task<IAnalysisResult> AnalyzeAsync(
        Stream logStream,
        Encoding logEncoding,
        IReadOnlyDictionary<string, object?> placeholders,
        DateTime? timestamp = null,
        CancellationToken cancellationToken = default
    );

    Task<string?> TryGetTitleAsync(Guid analysisId, CancellationToken cancellationToken = default);

    Task<(Stream Stream, Encoding Encoding)?> TryGetLogAsync(Guid analysisId, CancellationToken cancellationToken = default);

    Task<(Stream Stream, Encoding Encoding)?> TryGetSummaryAsync(Guid analysisId, CancellationToken cancellationToken = default);
}
