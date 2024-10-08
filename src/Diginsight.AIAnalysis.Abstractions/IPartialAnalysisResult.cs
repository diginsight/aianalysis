using System.Text;

namespace Diginsight.AIAnalysis;

public interface IPartialAnalysisResult
{
    Guid Id { get; }
    DateTime Timestamp { get; }

    Task<IAnalysisResult?> TryPromoteAsync(CancellationToken cancellationToken = default);

    Task<(Stream Stream, Encoding Encoding)> GetLogAsync(CancellationToken cancellationToken = default);
}
