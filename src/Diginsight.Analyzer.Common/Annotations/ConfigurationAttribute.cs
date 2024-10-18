using JetBrains.Annotations;

namespace Diginsight.Analyzer.Common.Annotations;

[AttributeUsage(AttributeTargets.Class)]
[MeansImplicitUse(
    ImplicitUseKindFlags.Assign | ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature,
    ImplicitUseTargetFlags.Members
)]
public sealed class ConfigurationAttribute : Attribute { }
