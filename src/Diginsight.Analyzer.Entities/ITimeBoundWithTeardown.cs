using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public interface ITimeBoundWithTeardown : ITimeBound
{
    [DisallowNull]
    DateTime? TeardownFinishedAt { get; set; }
}
