using Diginsight.Analyzer.Entities;
using JetBrains.Annotations;

namespace Diginsight.Analyzer.Business;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature, ImplicitUseTargetFlags.WithInheritors)]
public interface IAnalyzerStepExecutor
{
    string Template { get; }

    string InternalName { get; }

    string DisplayName { get; }

    bool DisableProgressFlushTimer => false;

    Task ExecuteAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken);

    Task TeardownAsync(IAnalysisContext analysisContext, CancellationToken cancellationToken);
}
