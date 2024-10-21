using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Diginsight.Analyzer.Business;

internal sealed partial class EventService : IEventService
{
    private readonly ILogger logger;
    private readonly IAmbientService ambientService;
    private readonly IEventMetaAccessor eventMetaAccessor;
    private readonly DaprClient? daprClient;
    private readonly Lazy<Task<bool>> isDaprEnabledLazy;

    public EventService(
        ILogger<EventService> logger,
        IAmbientService ambientService,
        IEventMetaAccessor eventMetaAccessor,
        DaprClient? daprClient = null
    )
    {
        this.logger = logger;
        this.ambientService = ambientService;
        this.eventMetaAccessor = eventMetaAccessor;
        this.daprClient = daprClient;

        isDaprEnabledLazy = daprClient is null
            ? new Lazy<Task<bool>>(Task.FromResult(false))
            : new Lazy<Task<bool>>(
                () => Task.Run(
                    async () =>
                    {
                        try
                        {
                            return await daprClient.CheckHealthAsync();
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                )
            );
    }

    public async Task EmitAsync(IEnumerable<string> recipients, Func<IReadOnlyDictionary<string, IEnumerable<string>>, DateTime, Event> makeEvent)
    {
        if (!await IsDaprEnabledAsync() || !recipients.Any())
        {
            return;
        }

        Event @event = makeEvent(eventMetaAccessor.Get(), ambientService.UtcNow);
        LogMessages.Emitting(logger, @event.EventKind, recipients);

        byte[] rawEvent;
        using (MemoryStream stream = new ())
        {
            await JsonSerializer.CreateDefault().SerializeAsync(stream, @event);
            rawEvent = stream.ToArray();
        }

        foreach (string recipient in recipients)
        {
            await daprClient!.PublishEventAsync(BusinessUtils.EventPubsubName, BusinessUtils.EventTopicPrefix + recipient, rawEvent);
        }
    }

    private async Task<bool> IsDaprEnabledAsync() => await isDaprEnabledLazy.Value;

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Trace, "Emitting {Kind} event to {Recipients}")]
        internal static partial void Emitting(ILogger logger, EventKind kind, IEnumerable<string> recipients);
    }
}
