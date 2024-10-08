using System.Text;

namespace Diginsight.AIAnalysis;

public interface IAnalysisResult : IPartialAnalysisResult
{
    string Title { get; }

    Task<(Stream Stream, Encoding Encoding)> GetSummaryAsync(CancellationToken cancellationToken = default);
}
