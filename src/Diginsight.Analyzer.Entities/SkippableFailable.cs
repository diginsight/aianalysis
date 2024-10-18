using Newtonsoft.Json;

namespace Diginsight.Analyzer.Entities;

public class SkippableFailable : IFailable, ISkippable
{
    private Exception? exception;
    private string? failReason;
    private string? skipReason;

    public SkippableFailable() { }

    [JsonConstructor]
    public SkippableFailable(Exception? exception, string? reason, bool isSkipped)
    {
        if (exception is not null)
        {
            Exception = exception;
        }
        else if (reason is not null)
        {
            if (isSkipped)
            {
                Skip(reason);
            }
            else
            {
                Fail(reason);
            }
        }
    }

    public bool IsFailed => Exception is not null || failReason is not null;

    public bool IsSkipped => skipReason is not null;

    public Exception? Exception
    {
        get => exception;
        set
        {
            CheckClear();
            exception = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public string? Reason => failReason ?? skipReason;

    public void Fail(string reason)
    {
        CheckClear();
        failReason = reason;
    }

    public void Skip(string reason)
    {
        CheckClear();
        skipReason = reason;
    }

    public virtual bool IsSucceeded() => !(IsFailed || IsSkipped);

    public Problem? ToProblem()
    {
        return IsFailed ? Exception is not null ? Problem.Failed(Exception) : Problem.Failed(Reason)
            : IsSkipped ? Problem.Skipped(Reason)
            : null;
    }

    protected void CheckClear()
    {
        if (IsFailed)
        {
            throw new InvalidOperationException("Already marked as failed");
        }

        if (IsSkipped)
        {
            throw new InvalidOperationException("Already marked as skipped");
        }
    }
}
