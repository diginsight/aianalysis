﻿using Diginsight.Analyzer.Entities;
using Diginsight.Analyzer.Repositories.Models;

namespace Diginsight.Analyzer.Business;

public interface ISnapshotService
{
    Task<(IEnumerable<AnalysisContextSnapshot> Items, int TotalCount)> GetAnalysesAsync(int page, int? pageSize, bool withProgress);

    Task<(IEnumerable<AnalysisContextSnapshot> Items, int TotalCount)> GetQueuedAnalysesAsync(int page, int? pageSize);

    Task<AnalysisContextSnapshot?> GetAnalysisAsync(Guid executionId, bool withProgress);

    Task<AnalysisContextSnapshot?> GetAnalysisAsync(AnalysisCoord analysisCoord, bool withProgress);
}
