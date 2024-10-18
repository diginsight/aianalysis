namespace Diginsight.Analyzer.Common;

public sealed class DependencyException<T> : ApplicationException
    where T : notnull
{
    public DependencyException(DependencyExceptionKind kind, params T[] keys)
        : base(kind.ToString("G"))
    {
        Kind = kind;
        Keys = keys;
    }

    public DependencyExceptionKind Kind { get; }

    public IEnumerable<T> Keys { get; }
}
