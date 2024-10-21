using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Business;

internal sealed class ReportService : IReportService
{
    private readonly ISnapshotService snapshotService;
    private readonly IReadOnlyDictionary<string, IAnalyzerStepTemplate> analyzerStepTemplates;

    public ReportService(
        ISnapshotService snapshotService,
        IEnumerable<IAnalyzerStepTemplate> analyzerStepTemplates
    )
    {
        this.snapshotService = snapshotService;
        this.analyzerStepTemplates = analyzerStepTemplates.ToDictionary(static x => x.Name);
    }

    public async Task<AnalysisReport?> GetReportAsync(Guid instanceId, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(instanceId, true) is { } analysisSnapshot
            ? await GetReportCoreAsync(analysisSnapshot, cancellationToken)
            : null;
    }

    public async Task<AnalysisReport?> GetReportAsync(AnalysisCoord analysisCoord, CancellationToken cancellationToken)
    {
        return await snapshotService.GetAnalysisAsync(analysisCoord, true) is { } analysisSnapshot
            ? await GetReportCoreAsync(analysisSnapshot, cancellationToken)
            : null;
    }

    private async Task<AnalysisReport?> GetReportCoreAsync(AnalysisContextSnapshot analysisSnapshot, CancellationToken cancellationToken)
    {
        Progress progress = analysisSnapshot.Progress!;
        return new AnalysisReport()
        {
            Steps = await analysisSnapshot.Steps.ToAsyncEnumerable()
                .Select(history => analyzerStepTemplates[history.Template].GetReport(history.InternalName, history.Status, progress))
                .ToArrayAsync(cancellationToken),
        };
    }
}
