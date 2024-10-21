using Diginsight.Analyzer.Business.Configurations;
using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories;
using Diginsight.Analyzer.Repositories.Models;
using Microsoft.Extensions.Options;

namespace Diginsight.Analyzer.Business;

internal sealed class SnapshotService : ISnapshotService
{
    private readonly IAnalysisInfoRepository analysisInfoRepository;
    private readonly IOptionsMonitor<CoreConfig> coreConfigMonitor;

    public SnapshotService(
        IAnalysisInfoRepository analysisInfoRepository,
        IOptionsMonitor<CoreConfig> coreConfigMonitor
    )
    {
        this.analysisInfoRepository = analysisInfoRepository;
        this.coreConfigMonitor = coreConfigMonitor;
    }

    public Task<(IEnumerable<AnalysisContextSnapshot> Items, int TotalCount)> GetAnalysesAsync(int page, int? pageSize, bool withProgress)
    {
        return analysisInfoRepository.GetAnalysisSnapshotsAsync(page, ValidatePageSize(pageSize), withProgress);
    }

    public Task<(IEnumerable<AnalysisContextSnapshot> Items, int TotalCount)> GetQueuedAnalysesAsync(int page, int? pageSize)
    {
        return analysisInfoRepository.GetQueuedAnalysisSnapshotsAsync(page, ValidatePageSize(pageSize));
    }

    public Task<AnalysisContextSnapshot?> GetAnalysisAsync(Guid executionId, bool withProgress)
    {
        return analysisInfoRepository.GetAnalysisSnapshotAsync(executionId, withProgress);
    }

    public Task<AnalysisContextSnapshot?> GetAnalysisAsync(AnalysisCoord analysisCoord, bool withProgress)
    {
        return analysisInfoRepository.GetAnalysisSnapshotAsync(analysisCoord, withProgress);
    }

    private int ValidatePageSize(int? pageSize)
    {
        ICoreConfig coreConfig = coreConfigMonitor.CurrentValue;

        int maxPageSize = coreConfig.MaxPageSize;
        if (pageSize > maxPageSize)
        {
            throw AnalysisExceptions.InputGreaterThan(nameof(pageSize), maxPageSize);
        }

        return pageSize ?? coreConfig.DefaultPageSize;
    }
}
