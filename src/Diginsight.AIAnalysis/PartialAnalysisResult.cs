using System.Text;

namespace Diginsight.AIAnalysis;

internal class PartialAnalysisResult : IPartialAnalysisResult
{
    protected readonly IAnalysisService analysisService;

    private IAnalysisResult? promoted;

    public Guid Id { get; }
    public DateTime Timestamp { get; }

    protected internal PartialAnalysisResult(IAnalysisService analysisService, Guid id, DateTime timestamp)
    {
        this.analysisService = analysisService;

        Id = id;
        Timestamp = timestamp;
    }

    public virtual async Task<IAnalysisResult?> TryPromoteAsync(CancellationToken cancellationToken)
    {
        return promoted ??=
            await analysisService.TryGetTitleAsync(Id, cancellationToken) is { } title
                ? new AnalysisResult(analysisService, Id, Timestamp, title)
                : null;
    }

    public async Task<(Stream Stream, Encoding Encoding)> GetLogAsync(CancellationToken cancellationToken)
    {
        return (await analysisService.TryGetLogAsync(Id, cancellationToken))!.Value;
    }
}
