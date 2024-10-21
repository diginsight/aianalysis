using Diginsight.Analyzer.Entities;

namespace Diginsight.Analyzer.Business;

public interface IAnalysisService
{
    Task<IEnumerable<AnalysisCoord>> AbortAsync(Guid? executionId);

    Task<IEnumerable<AnalysisCoord>> AbortAsync(Guid? analysisId, int? attempt);
}
