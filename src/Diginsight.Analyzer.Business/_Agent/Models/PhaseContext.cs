namespace Diginsight.Analyzer.Business.Models;

internal abstract class PhaseContext : Failable, IPhaseContext
{
    private readonly IReadOnlyDictionary<string, (StepHistory Item, int Index)> stepHistories;
    private Guid? persistenceId;

    protected PhaseContext(IEnumerable<string> stepNames)
    {
        stepHistories = stepNames
            .Select(static (n, i) => (n, i))
            .ToDictionary(static x => x.n, static x => (new StepHistory(x.n), x.i));
    }

    public Guid PersistenceId => persistenceId ??= Guid.NewGuid();

    public IEnumerable<StepHistory> StepHistories => stepHistories.Values.OrderBy(static x => x.Index).Select(static x => x.Item).ToArray();

    public StepHistory GetStepHistory(string stepName)
    {
        return stepHistories[stepName].Item;
    }

    public abstract bool IsNotStarted();

    public abstract TResult Accept<TResult, TArg>(IPhaseContextVisitor<TResult, TArg> visitor, TArg arg);
}
