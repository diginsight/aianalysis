namespace Diginsight.Analyzer.Entities;

[Flags]
public enum QueuingPolicy
{
    Never = 0,
    IfFull = 1 << 0,
    IfConflict = 1 << 1,
    IfFullOrConflict = IfFull | IfConflict,
    IfConflictOrFull = IfFullOrConflict,
}
