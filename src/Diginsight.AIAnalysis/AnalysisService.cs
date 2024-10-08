using System.Text;

namespace Diginsight.AIAnalysis;

internal sealed class AnalysisService : IAnalysisService
{
    private readonly IInternalAnalysisService internalAnalysisService;

    public AnalysisService(IInternalAnalysisService internalAnalysisService)
    {
        this.internalAnalysisService = internalAnalysisService;
    }

    public async Task<IPartialAnalysisResult> StartAnalyzeAsync(
        Stream logStream,
        Encoding logEncoding,
        IReadOnlyDictionary<string, object?> placeholders,
        DateTime? timestamp,
        CancellationToken cancellationToken
    )
    {
        (IPartialAnalysisResult partialAnalysisResult, string logContent) =
            await CoreStartAnalyzeAsync(logStream, logEncoding, timestamp, cancellationToken);

        TaskUtils.RunAndForget(
            () => CoreEndAnalyzeAsync(partialAnalysisResult, logContent, placeholders, CancellationToken.None),
            cancellationToken
        );

        return partialAnalysisResult;
    }

    public async Task<IAnalysisResult> AnalyzeAsync(
        Stream logStream,
        Encoding logEncoding,
        IReadOnlyDictionary<string, object?> placeholders,
        DateTime? timestamp,
        CancellationToken cancellationToken
    )
    {
        (IPartialAnalysisResult partialAnalysisResult, string logContent) =
            await CoreStartAnalyzeAsync(logStream, logEncoding, timestamp, cancellationToken);

        return await CoreEndAnalyzeAsync(partialAnalysisResult, logContent, placeholders, cancellationToken);
    }

    private async Task<(IPartialAnalysisResult Result, string LogContent)> CoreStartAnalyzeAsync(
        Stream logStream, Encoding logEncoding, DateTime? timestamp, CancellationToken cancellationToken
    )
    {
        internalAnalysisService.LabelAnalysis(timestamp, out DateTime finalTimestamp, out Guid analysisId);

        string logContent;
        using (MemoryStream tempLogStream = new ())
        {
            await logStream.CopyToAsync(tempLogStream, cancellationToken);

            tempLogStream.Position = 0;
            using (TextReader logTextReader =
#if NET
                new StreamReader(tempLogStream, logEncoding, leaveOpen: true)
#else
                new StreamReader(tempLogStream, logEncoding, true, 1024, true)
#endif
            )
            {
                logContent = await logTextReader.ReadToEndAsync(cancellationToken);
            }

            tempLogStream.Position = 0;
            await internalAnalysisService.WriteLogAsync(finalTimestamp, analysisId, tempLogStream, cancellationToken);
        }

        IPartialAnalysisResult partialAnalysisResult = new PartialAnalysisResult(this, analysisId, finalTimestamp);

        return (partialAnalysisResult, logContent);
    }

    private async Task<IAnalysisResult> CoreEndAnalyzeAsync(
        IPartialAnalysisResult partialAnalysisResult,
        string logContent,
        IReadOnlyDictionary<string, object?> placeholders,
        CancellationToken cancellationToken
    )
    {
        DateTime timestamp = partialAnalysisResult.Timestamp;
        Guid analysisId = partialAnalysisResult.Id;

        string title = await internalAnalysisService.AnalyzeAsync(timestamp, analysisId, logContent, placeholders, cancellationToken);
        await internalAnalysisService.ConsolidateAsync(analysisId, title, cancellationToken);

        return new AnalysisResult(this, analysisId, timestamp, title);
    }

    public Task<string?> TryGetTitleAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        return internalAnalysisService.TryGetTitleAsync(analysisId, cancellationToken);
    }

    public Task<(Stream Stream, Encoding Encoding)?> TryGetLogAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        return internalAnalysisService.TryGetLogAsync(analysisId, cancellationToken);
    }

    public Task<(Stream Stream, Encoding Encoding)?> TryGetSummaryAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        return internalAnalysisService.TryGetSummaryAsync(analysisId, cancellationToken);
    }
}
