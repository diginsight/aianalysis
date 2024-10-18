using JetBrains.Annotations;

namespace Diginsight.Analyzer.Entities;

[UsedImplicitly(
    ImplicitUseKindFlags.Access,
    ImplicitUseTargetFlags.Members | ImplicitUseTargetFlags.WithInheritors
)]
public interface IAnalyzed
{
    bool IsSucceeded();

    Problem? ToProblem();
}
