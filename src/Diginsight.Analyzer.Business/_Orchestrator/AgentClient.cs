using Diginsight.Analyzer.Business.Models;
using Newtonsoft.Json;
using System.Net;
using System.Runtime.ExceptionServices;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentClient : ClientBase, IAgentClient
{
    public AgentClient(IHttpClientFactory httpClientFactory, Uri baseAddress)
        : base(MakeClient(httpClientFactory, baseAddress)) { }

    public Task<StartResponseBody> StartMigrationAsync(StartMigrationRequestBody body, IEnumerable<string> eventRecipients)
    {
        return InvokeAgentAsync<StartResponseBody>(HttpMethod.Post, "migrate/start", true, eventRecipients, body);
    }

    public Task DequeueMigrationAsync(Guid instanceId)
    {
        return InvokeAgentAsync(HttpMethod.Post, $"migrate/dequeue/{instanceId:D}", false);
    }

    public Task<StartResponseBody> StartDeletionAsync(StartDeletionRequestBody body, IEnumerable<string> eventRecipients)
    {
        return InvokeAgentAsync<StartResponseBody>(HttpMethod.Post, "delete/start", true, eventRecipients, body);
    }

    public Task<AbortResponseBody> AbortMigrationAsync(Guid? instanceId)
    {
        return InvokeAgentAsync<AbortResponseBody>(HttpMethod.Post, $"migrate/abort/{instanceId:D}", false);
    }

    public Task<AbortResponseBody> AbortDeletionAsync(Guid? instanceId)
    {
        return InvokeAgentAsync<AbortResponseBody>(HttpMethod.Post, $"delete/abort/{instanceId:D}", false);
    }

    private static HttpClient MakeClient(IHttpClientFactory httpClientFactory, Uri baseAddress)
    {
        HttpClient httpClient = httpClientFactory.CreateClient(typeof(AgentClient).FullName!);
        httpClient.BaseAddress = baseAddress;
        return httpClient;
    }

    private static async Task<T> CoreInvokeAgentAsync<T>(Func<Task<T>> runAsync)
    {
        try
        {
            return await runAsync();
        }
        catch (TaskCanceledException exception) when (exception.InnerException is TimeoutException timeoutException)
        {
            ExceptionDispatchInfo.Throw(timeoutException);
            throw timeoutException;
        }
        catch (DownstreamApiException exception)
        {
            HttpStatusCode statusCode = exception.StatusCode;

            ExceptionView? exceptionView;
            using (MemoryStream stream = new (exception.RawContent))
            {
                try
                {
                    exceptionView = JsonSerializer.CreateDefault().Deserialize<ExceptionView>(stream);
                }
                catch (JsonException)
                {
                    exceptionView = null;
                }
            }

            if (exceptionView is null)
            {
                throw MigrationExceptions.DownstreamException($"Received {statusCode} invoking agent");
            }

            string message = exceptionView.Message;
            throw MigrationExceptions.DownstreamException(
                $"Received {statusCode} invoking agent: {message}",
                exceptionView.Label is { } label ? new MigrationException(message, statusCode, label, exceptionView.Parameters!) : null
            );
        }
    }

    private static IEnumerable<KeyValuePair<string, object?>> MakeRequestOptions(bool freshStart, IEnumerable<string>? eventRecipients)
    {
        return new[]
        {
            new KeyValuePair<string, object?>(AgentDelegatingHandler.FreshStartOptionsKey.Key, freshStart),
            new KeyValuePair<string, object?>(AgentDelegatingHandler.EventRecipientsOptionsKey.Key, eventRecipients ?? Enumerable.Empty<string>()),
        };
    }

    private Task<T> InvokeAgentAsync<T>(HttpMethod method, string uri, bool freshStart, IEnumerable<string>? eventRecipients = null, object? body = null)
    {
        return CoreInvokeAgentAsync(() => InvokeAsync<T>(method, uri, MakeRequestOptions(freshStart, eventRecipients), body));
    }

    private Task InvokeAgentAsync(HttpMethod method, string uri, bool freshStart, IEnumerable<string>? eventRecipients = null, object? body = null)
    {
        return CoreInvokeAgentAsync<ValueTuple>(
            async () =>
            {
                await InvokeAsync(method, uri, MakeRequestOptions(freshStart, eventRecipients), body);
                return default;
            }
        );
    }

    [JsonObject(MissingMemberHandling = MissingMemberHandling.Error)]
    private sealed record ExceptionView(
        string Message,
        ExceptionView? InnerException,
        string? Label,
        object?[]? Parameters
    );
}
