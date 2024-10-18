using JetBrains.Annotations;

namespace Diginsight.Analyzer.Common.Annotations;

[AttributeUsage(AttributeTargets.Class)]
[MeansImplicitUse(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public sealed class InjectableAttribute : Attribute { }
