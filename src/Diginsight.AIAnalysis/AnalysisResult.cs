using System.Text;

namespace Diginsight.AIAnalysis;

internal sealed class AnalysisResult : PartialAnalysisResult, IAnalysisResult
{
    public string Title { get; }

    internal AnalysisResult(IAnalysisService analysisService, Guid id, DateTime timestamp, string title)
        : base(analysisService, id, timestamp)
    {
        Title = title;
    }

    public override Task<IAnalysisResult?> TryPromoteAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult<IAnalysisResult?>(this);
    }

    public async Task<(Stream Stream, Encoding Encoding)> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        return (await analysisService.TryGetSummaryAsync(Id, cancellationToken))!.Value;
    }
}
