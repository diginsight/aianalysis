using Diginsight.Analyzer.Entities;

namespace Diginsight.Analyzer.Business;

public interface IReportService
{
    Task<AnalysisReport?> GetReportAsync(Guid executionId, CancellationToken cancellationToken);

    Task<AnalysisReport?> GetReportAsync(AnalysisCoord analysisCoord, CancellationToken cancellationToken);
}
