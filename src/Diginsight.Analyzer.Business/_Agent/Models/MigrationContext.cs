using Newtonsoft.Json;

namespace Diginsight.Analyzer.Business.Models;

internal sealed class MigrationContext : PhaseContext, IMigrationContext
{
    private readonly IReadOnlyDictionary<Guid, ISiteContext> siteContexts;

    [JsonProperty]
    private readonly DateTime? startedAt;

    [JsonProperty]
    private readonly string? machineName;

    private GlobalProgress progress = new ();

    public MigrationContext(
        Guid instanceId,
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        IEnumerable<string> globalStepNames,
        IEnumerable<string> siteStepNames,
        DateTime? queuedAt,
        string family,
        DequeuingInfo? dequeuingInfo = null,
        DateTime? startedAt = null,
        string? machineName = null
    )
        : base(globalStepNames)
    {
        InstanceId = instanceId;
        GlobalInfo = globalInfo;
        Sites = sites;
        QueuedAt = queuedAt;
        Family = family;
        DequeuingInfo = dequeuingInfo;
        this.startedAt = startedAt;
        this.machineName = machineName;

        siteContexts = Sites.ToDictionary(
            static x => x.Key,
            x => (ISiteContext)new SiteContext(this, x.Key, x.Value, siteStepNames)
        );
    }

    public Guid InstanceId { get; }

    public GlobalInfo GlobalInfo { get; }

    public IReadOnlyDictionary<Guid, SiteInfo> Sites { get; }

    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public DequeuingInfo? DequeuingInfo { get; }

    public DateTime? QueuedAt { get; }

    [JsonIgnore]
    public DateTime StartedAt => startedAt ?? throw new InvalidOperationException("Not started");

    public DateTime? FinishedAt { get; set; }

    [JsonIgnore]
    public string MachineName => machineName ?? throw new InvalidOperationException("Not started");

    public string Family { get; }

    public TimeBoundStatus Status { get; set; }

    public T GetProgress<T>()
        where T : GlobalProgress, new()
    {
        T expanded = progress.As<T>();
        progress = expanded;
        return expanded;
    }

    public ISiteContext GetSiteContext(Guid siteId) => siteContexts[siteId];

    public IEnumerable<ISiteContext> GetSiteContexts() => siteContexts.Values;

    public override bool IsNotStarted() => startedAt is null;

    public override TResult Accept<TResult, TArg>(IPhaseContextVisitor<TResult, TArg> visitor, TArg arg)
    {
        return visitor.Visit(this, arg);
    }
}
