using JetBrains.Annotations;

namespace Diginsight.Analyzer.Common.Annotations;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
[MeansImplicitUse(
    ImplicitUseKindFlags.Assign | ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature,
    ImplicitUseTargetFlags.Members
)]
public sealed class DeserializedAttribute : Attribute { }
