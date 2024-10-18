using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public class Skippable : ISkippable
{
    public Skippable() { }

    [JsonConstructor]
    public Skippable(string? reason)
    {
        if (reason is not null)
        {
            Skip(reason);
        }
    }

    public bool IsSkipped => Reason is not null;

    public string? Reason { get; private set; }

    public void Skip(string reason)
    {
        CheckClear();
        Reason = reason;
    }

    public virtual bool IsSucceeded() => !IsSkipped;

    public Problem? ToProblem()
    {
        return IsSkipped ? Problem.Skipped(Reason) : null;
    }

    protected void CheckClear()
    {
        if (IsSkipped)
        {
            throw new InvalidOperationException("Already marked as skipped");
        }
    }
}
