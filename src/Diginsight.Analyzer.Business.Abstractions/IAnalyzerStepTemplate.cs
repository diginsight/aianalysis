using Diginsight.Analyzer.Business.Models;
using Diginsight.Analyzer.Entities;
using JetBrains.Annotations;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature, ImplicitUseTargetFlags.WithInheritors)]
public interface IAnalyzerStepTemplate
{
    string Name { get; }

    Task ValidateAsync(StrongBox<GlobalInput> globalInputBox, StrongBox<StepInput> stepInputBox, CancellationToken cancellationToken);

    Task<bool> HasConflictAsync(GlobalInput globalInput, IEnumerable<StepInstance> steps, AnalysisLease lease, CancellationToken cancellationToken);

    IAnalyzerStepExecutor CreateExecutor(StepInstance instance, IServiceProvider serviceProvider);

    StepReport GetReport(string internalName, TimeBoundStatus status, Progress progress) => new (internalName, status);

    protected static StepReport GetReport<TId>(
        string name, TimeBoundStatus status, IEnumerable<KeyValuePair<TId, IAnalyzed>> analyzedItems
    )
        where TId : notnull
    {
        return new StepReport<TId>(
            name,
            status,
            analyzedItems
                .Select(static x => (x.Key, Problem: x.Value.ToProblem()))
                .Where(static x => x.Problem is not null)
                .ToDictionary(static x => x.Key, static x => x.Problem!)
        );
    }

    protected static StepReport GetReport<TId, TItem>(
        string name, TimeBoundStatus status, IEnumerable<KeyValuePair<TId, TItem>> analyzedItems
    )
        where TId : notnull
        where TItem : IAnalyzed
    {
        return GetReport(name, status, analyzedItems.Select(static x => KeyValuePair.Create(x.Key, (IAnalyzed)x.Value)));
    }
}
