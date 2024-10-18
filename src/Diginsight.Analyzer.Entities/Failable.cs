using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public class Failable : IFailable
{
    private Exception? exception;

    public Failable() { }

    [JsonConstructor]
    public Failable(Exception? exception, string? reason)
    {
        if (exception is not null)
        {
            Exception = exception;
        }
        else if (reason is not null)
        {
            Fail(reason);
        }
    }

    public bool IsFailed => Exception is not null || Reason is not null;

    [DisallowNull]
    public Exception? Exception
    {
        get => exception;
        set
        {
            CheckClear();
            exception = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public string? Reason { get; private set; }

    public void Fail(string reason)
    {
        CheckClear();
        Reason = reason;
    }

    public virtual bool IsSucceeded() => !IsFailed;

    public Problem? ToProblem()
    {
        return IsFailed ? Exception is not null ? Problem.Failed(Exception) : Problem.Failed(Reason) : null;
    }

    protected void CheckClear()
    {
        if (IsFailed)
        {
            throw new InvalidOperationException("Already marked as failed");
        }
    }
}
