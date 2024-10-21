using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public class GlobalInput : Expandable<GlobalInput>
{
    [JsonProperty]
    private JArray? eventRecipients;

    public int Parallelism { get; set; }

    [JsonIgnore]
    [AllowNull]
    public IEnumerable<EventRecipient> EventRecipients
    {
        get => (eventRecipients ??= [ ])
            .AsEnumerable()
            .Select(static jt => jt.TryToObject(out string? str) ? new EventRecipient() { Name = str! } : jt.ToObject<EventRecipient>()!);
        set => eventRecipients = value is null ? null : JArray.FromObject(value);
    }
}
