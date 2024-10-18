using JetBrains.Annotations;

namespace Diginsight.Analyzer.Common.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
[MeansImplicitUse(
    ImplicitUseKindFlags.Access,
    ImplicitUseTargetFlags.Members | ImplicitUseTargetFlags.WithInheritors
)]
public sealed class SerializedAttribute : Attribute { }
