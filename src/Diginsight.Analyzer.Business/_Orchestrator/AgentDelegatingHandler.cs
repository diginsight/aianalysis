using System.Globalization;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentDelegatingHandler : DelegatingHandler
{
    public static readonly HttpRequestOptionsKey<bool> FreshStartOptionsKey = new ("freshStart");
    public static readonly HttpRequestOptionsKey<IEnumerable<string>> EventRecipientsOptionsKey = new ("eventRecipients");

    private readonly IParallelismSettingsAccessor parallelismSettingsAccessor;
    private readonly IEventMetaAccessor eventMetaAccessor;

    public AgentDelegatingHandler(
        IParallelismSettingsAccessor parallelismSettingsAccessor,
        IEventMetaAccessor eventMetaAccessor
    )
    {
        this.parallelismSettingsAccessor = parallelismSettingsAccessor;
        this.eventMetaAccessor = eventMetaAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _ = request.Options.TryGetValue(FreshStartOptionsKey, out bool freshStart);
        if (freshStart)
        {
            foreach ((string[] labels, int parallelism) in parallelismSettingsAccessor.Get())
            {
                request.Headers.Add(
                    string.Join('-', labels.Prepend(HeaderParallelismSettings.Prefix)),
                    parallelism.ToString(CultureInfo.InvariantCulture)
                );
            }

            if (request.Options.TryGetValue(EventRecipientsOptionsKey, out IEnumerable<string>? eventRecipients))
            {
                request.Headers.Add(BusinessUtils.EventRecipientHeader, eventRecipients);
            }

            IEnumerable<(string, string)> emkvs = eventMetaAccessor.Get()
                .SelectMany(
                    static emkvs =>
                    {
                        string emk = emkvs.Key;
                        return emkvs.Value.Select(emv => (emk, emv));
                    }
                );
            foreach ((string emk, string emv) in emkvs)
            {
                request.Headers.Add(HeaderEventMeta.PrefixDash + emk, emv);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
