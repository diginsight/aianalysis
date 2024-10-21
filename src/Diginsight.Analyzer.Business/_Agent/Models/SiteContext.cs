namespace Diginsight.Analyzer.Business.Models;

internal sealed class SiteContext : PhaseContext, ISiteContext
{
    private SiteProgress progress = new ();

    public SiteContext(
        IMigrationContext globalContext,
        Guid siteId,
        SiteInfo siteInfo,
        IEnumerable<string> siteStepNames
    )
        : base(siteStepNames)
    {
        GlobalContext = globalContext;
        SiteId = siteId;
        SiteInfo = siteInfo;
    }

    public IMigrationContext GlobalContext { get; }

    public Guid SiteId { get; }

    public SiteInfo SiteInfo { get; }

    public T GetProgress<T>()
        where T : SiteProgress, new()
    {
        T expanded = progress.As<T>();
        progress = expanded;

        if (!progress.OrganizationId.HasValue && Guid.TryParse(SiteInfo.OrganizationId, out Guid oId))
        {
            Console.WriteLine($"{typeof(T).Name} process OrganizationId is null");
            progress.OrganizationId = oId;
        }

        return expanded;
    }

    public override bool IsNotStarted() => GlobalContext.IsNotStarted();

    public override TResult Accept<TResult, TArg>(IPhaseContextVisitor<TResult, TArg> visitor, TArg arg)
    {
        return visitor.Visit(this, arg);
    }
}
