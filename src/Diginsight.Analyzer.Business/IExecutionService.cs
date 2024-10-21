using Diginsight.Analyzer.Entities;

namespace Diginsight.Analyzer.Business;

internal interface IExecutionService
{
    Task<IEnumerable<Guid>> AbortAsync(ExecutionKind kind, Guid? executionId);
}
