using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Repositories;

public interface IAnalysisInfoRepository
{
    Task InsertAsync(IAnalysisContext analysisContext);

    Task UpsertAsync(IAnalysisContext analysisContext);

    Task DeleteAsync(Guid executionId);

    IDisposable? StartTimedProgressFlush(IAnalysisContext analysisContext);

    Task<(IEnumerable<AnalysisContextSnapshot> Items, int TotalCount)> GetAnalysisSnapshotsAsync(int page, int pageSize, bool withProgress);

    Task<(IEnumerable<AnalysisContextSnapshot> Items, int TotalCount)> GetQueuedAnalysisSnapshotsAsync(int page, int pageSize);

    Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(Guid executionId, bool withProgress);

    Task<AnalysisContextSnapshot?> GetAnalysisSnapshotAsync(AnalysisCoord analysisCoord, bool withProgress);

    IAsyncEnumerable<AnalysisContextSnapshot> GetAllQueuedAnalysisSnapshotsAE();
}
