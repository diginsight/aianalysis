using Diginsight.Analyzer.Business.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal sealed partial class MailerService : IMailerService
{
    private readonly ILogger logger;
    private readonly IReportService reportService;
    private readonly IEnumerable<IMailProvider> mailProviders;
    private readonly IOptionsMonitor<CoreConfig> coreConfigMonitor;

    public MailerService(
        ILogger<MailerService> logger,
        IReportService reportService,
        IEnumerable<IMailProvider> mailProviders,
        IOptionsMonitor<CoreConfig> coreConfigMonitor
    )
    {
        this.logger = logger;
        this.reportService = reportService;
        this.mailProviders = mailProviders;
        this.coreConfigMonitor = coreConfigMonitor;
    }

    private IOrchestratorCoreConfig CoreConfig => coreConfigMonitor.CurrentValue;

    public async Task SendIfNeededAsync(JObject rawEvent)
    {
        PartialEvent partialEvent;
        try
        {
            partialEvent = rawEvent.ToObject<PartialEvent>()!;
        }
        catch (JsonException exception)
        {
            LogMessages.InvalidEventReceived(logger, exception);
            return;
        }

        EventKind eventKind = partialEvent.EventKind;
        ExecutionKind executionKind = partialEvent.ExecutionKind;
        Guid instanceId = partialEvent.InstanceId;

        using IDisposable? d0 = new LogScopeBuilder()
            .With("EventKind", eventKind)
            .With("ExecutionKind", executionKind)
            .With("InstanceId", instanceId)
            .Begin(logger);
        LogMessages.EventReceived(logger);

        if (await CoreSendIfNeededAsync())
        {
            LogMessages.EmailSent(logger);
        }
        else
        {
            LogMessages.EventIgnored(logger);
        }

        async Task<bool> CoreSendIfNeededAsync()
        {
            if (CoreConfig.AllowAllEventsNotification)
            {
                NotificationMode notificationMode = partialEvent.Metadata.TryGetValue("nm", out IEnumerable<string>? nms)
                    && Enum.TryParse(nms.LastOrDefault(), true, out NotificationMode nm)
                        ? nm : default;
                if (notificationMode == NotificationMode.StartAndFinish &&
                    eventKind is not (EventKind.MigrationStarted or EventKind.MigrationFinished or EventKind.DeletionStarted or EventKind.DeletionFinished))
                {
                    return false;
                }
            }
            else
            {
                if (eventKind is not (EventKind.MigrationStarted or EventKind.MigrationFinished or EventKind.DeletionStarted or EventKind.DeletionFinished))
                {
                    return false;
                }
            }

            IEnumerable<string> to = GetRecipients("to");
            IEnumerable<string> cc = GetRecipients("cc");
            IEnumerable<string> bcc = GetRecipients("bcc");

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            IEnumerable<string> GetRecipients(string k)
            {
                return partialEvent.Metadata.TryGetValue(k, out IEnumerable<string>? v) ? v.Distinct() : Enumerable.Empty<string>();
            }

            if (!to.Union(cc).Union(bcc).Any())
            {
                return false;
            }

            string timestampStr = $"{partialEvent.Timestamp:yyyy-MM-dd' 'HH:mm:ss} UTC";

            string subject;
            string plainContent;
            string htmlContent;
            ICollection<Attachment> attachments = new List<Attachment>();

            switch (eventKind)
            {
                case EventKind.MigrationStarted:
                {
                    MigrationStartedEvent @event = rawEvent.ToObject<MigrationStartedEvent>()!;
                    if (!@event.Queued)
                    {
                        return false;
                    }

                    subject = "Migration started";
                    plainContent = $"Migration {instanceId} started at {timestampStr}";
                    htmlContent = $"Migration <b>{instanceId}</b> started at <b>{timestampStr}</b>";

                    break;
                }

                case EventKind.MigrationFinished:
                {
                    MigrationFinishedEvent @event = rawEvent.ToObject<MigrationFinishedEvent>()!;

                    subject = "Migration finished";
                    plainContent = $"""
                                    Migration {instanceId} finished at {timestampStr}
                                    Status: {@event.Status:G}
                                    """;
                    htmlContent = $"""
                                   Migration <b>{instanceId}</b> finished at <b>{timestampStr}</b><br>
                                   Status: {WithColor(@event.Status)}
                                   """;

                    MigrationReport report = (await reportService.GetReportAsync(instanceId, CancellationToken.None))!;
                    byte[] zipContent;
                    await using (MemoryStream zipStream = new ())
                    {
                        using (ZipArchive archive = new (zipStream, ZipArchiveMode.Update, leaveOpen: true))
                        {
                            ZipArchiveEntry entry = archive.CreateEntry($"Migration report {instanceId}.json");
                            await using Stream entryStream = entry.Open();

                            JsonSerializer serializer = JsonSerializer.CreateDefault(new JsonSerializerSettings() { Formatting = Formatting.Indented });
                            await serializer.SerializeAsync(entryStream, report);
                        }

                        zipContent = zipStream.ToArray();
                    }

                    attachments.Add(new Attachment($"Migration report {instanceId}.zip", zipContent, "application/zip"));

                    break;
                }

                case EventKind.DeletionStarted:
                {
                    DeletionStartedEvent @event = rawEvent.ToObject<DeletionStartedEvent>()!;
                    if (!@event.Queued)
                    {
                        return false;
                    }

                    subject = "Deletion started";
                    plainContent = $"Deletion {instanceId} of site {@event.SiteId:D} started at {timestampStr}";
                    htmlContent = $"Deletion <b>{instanceId}</b> of site <b>{@event.SiteId:D}</b> started at <b>{timestampStr}</b>";

                    break;
                }

                case EventKind.DeletionFinished:
                {
                    DeletionFinishedEvent @event = rawEvent.ToObject<DeletionFinishedEvent>()!;

                    subject = "Deletion finished";
                    plainContent = $"""
                                    Deletion {instanceId} of site {@event.SiteId:D} finished at {timestampStr}
                                    Succeeded? {(@event.Succeeded ? "Yes" : "No")}
                                    """;
                    htmlContent = $"""
                                   Deletion <b>{instanceId}</b> of site <b>{@event.SiteId:D}</b> finished at <b>{timestampStr}</b><br>
                                   Succeeded? {(@event.Succeeded ? "<span style=\"font-color: green\">Yes</span>" : "<span style=\"font-color: red\">No</span>")}
                                   """;

                    break;
                }

                case EventKind.StepStarted:
                {
                    StepStartedEvent @event = rawEvent.ToObject<StepStartedEvent>()!;

                    subject = "Step started";

                    string description;
                    string htmlDescription;
                    if (@event.IsAfter)
                    {
                        description = "started \"after-phase\"";
                        htmlDescription = "started &quot;after-phase&quot;";
                    }
                    else
                    {
                        description = htmlDescription = "started";
                    }

                    if (@event.SiteId is { } siteId)
                    {
                        plainContent = $"""
                                        Progress of migration {instanceId}
                                        Site step '{@event.Name}' {description} on site {siteId:D} at {timestampStr}
                                        """;
                        htmlContent = $"""
                                       Progress of migration <b>{instanceId}</b><br>
                                       Site step <b>{@event.Name}</b> {htmlDescription} on site <b>{siteId:D}</b> at <b>{timestampStr}</b>
                                       """;
                    }
                    else
                    {
                        plainContent = $"""
                                        Progress of migration {instanceId}
                                        Global step '{@event.Name}' {description} at {timestampStr}
                                        """;
                        htmlContent = $"""
                                       Progress of migration {instanceId}<br>
                                       Global step <b>{@event.Name}</b> {htmlDescription} at <b>{timestampStr}</b>
                                       """;
                    }

                    break;
                }

                case EventKind.StepFinished:
                {
                    StepFinishedEvent @event = rawEvent.ToObject<StepFinishedEvent>()!;

                    subject = "Step finished";

                    string description;
                    string htmlDescription;
                    if (@event is { IsAfter: false, Status: FinishedEventStatus.Completed })
                    {
                        description = "finished \"before-phase\"";
                        htmlDescription = "finished &quot;before-phase&quot;";
                    }
                    else
                    {
                        description = htmlDescription = "finished";
                    }

                    if (@event.SiteId is { } siteId)
                    {
                        plainContent = $"""
                                        Progress of migration {instanceId}
                                        Site step '{@event.Name}' {description} on site {siteId:D} at {timestampStr}
                                        Status: {@event.Status:G}
                                        """;
                        htmlContent = $"""
                                       Progress of migration <b>{instanceId}</b><br>
                                       Site step <b>{@event.Name}</b> {htmlDescription} on site <b>{siteId:D}</b> at <b>{timestampStr}</b><br>
                                       Status: {WithColor(@event.Status)}
                                       """;
                    }
                    else
                    {
                        plainContent = $"""
                                        Progress of migration {instanceId}
                                        Global step '{@event.Name}' {description} at {timestampStr}
                                        Status: {@event.Status:G}
                                        """;
                        htmlContent = $"""
                                       Progress of migration <b>{instanceId}</b><br>
                                       Global step <b>{@event.Name}</b> {description} at <b>{timestampStr}</b><br>
                                       Status: {WithColor(@event.Status)}
                                       """;
                    }

                    break;
                }

                case EventKind.StepCustom:
                    return false;

                default:
                    throw new UnreachableException($"unrecognized {nameof(EventKind)}");
            }

            static string WithColor(FinishedEventStatus status)
            {
                string color = status switch
                {
                    FinishedEventStatus.Completed => "green",
                    FinishedEventStatus.Failed => "red",
                    FinishedEventStatus.Aborted => "olive",
                    _ => throw new UnreachableException($"unrecognized {nameof(FinishedEventStatus)}"),
                };
                return $"<span style=\"color: {color}\">{status:G}</span>";
            }

            foreach (IMailProvider mailProvider in mailProviders)
            {
                await mailProvider.SendAsync(
                    CoreConfig.MailFrom,
                    to,
                    cc,
                    bcc,
                    $"ELCPV2 Migration Tool - {subject}",
                    plainContent,
                    htmlContent,
                    attachments
                );
            }

            return true;
        }
    }

    [JsonObject(ItemRequired = Required.AllowNull)]
    private sealed class PartialEvent : Event
    {
        [JsonConstructor]
        private PartialEvent(EventKind eventKind, ExecutionKind executionKind)
        {
            EventKind = eventKind;
            ExecutionKind = executionKind;
            Metadata = new Dictionary<string, IEnumerable<string>>();
        }

        public override EventKind EventKind { get; }

        public override ExecutionKind ExecutionKind { get; }
    }

#pragma warning disable SA1201
    private enum NotificationMode : byte
    {
        StartAndFinish,
        Everything,
    }
#pragma warning restore SA1201

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Warning, "Invalid event received")]
        internal static partial void InvalidEventReceived(ILogger logger, Exception exception);

        [LoggerMessage(1, LogLevel.Debug, "Event received")]
        internal static partial void EventReceived(ILogger logger);

        [LoggerMessage(2, LogLevel.Debug, "Email notification sent")]
        internal static partial void EmailSent(ILogger logger);

        [LoggerMessage(3, LogLevel.Debug, "Event ignored")]
        internal static partial void EventIgnored(ILogger logger);
    }
}
