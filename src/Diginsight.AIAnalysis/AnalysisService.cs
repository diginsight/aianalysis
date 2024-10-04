using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Diginsight.AIAnalysis;

internal sealed class AnalysisService : IAnalysisService
{
    private const string AnalysisIdTagKey = "aid";
    private const string ExtensionTagKey = "ext";
    private const string TimestampMetadataKey = "ts";
    private const string FormatMetadataKey = "fmt";

    private const string LogExtension = "log";
    private const string SummaryExtension = "html";

    private static readonly Regex GoodPlaceholderRegex = new ("%([^,:%]+?)(,\\d+?)?(:[^%]+?)?%");
    private static readonly Regex BadPlaceholderRegex = new ("%([^%]+?)%");

    private readonly TimeProvider timeProvider;
    private readonly ChatClient chatClient;
    private readonly BlobContainerClient blobContainerClient;
    private readonly string untitledBlobNameFormat;
    private readonly string titledBlobNameFormat;

    private readonly Lazy<Func<IReadOnlyDictionary<string, object?>, IEnumerable<ChatMessage>>> makeChatMessagesLazy;

    private Func<IReadOnlyDictionary<string, object?>, IEnumerable<ChatMessage>> MakeChatMessages => makeChatMessagesLazy.Value;

    public AnalysisService(
        TimeProvider timeProvider,
        IOptions<AnalysisOptions> analysisOptions
    )
    {
        IAnalysisOptions analysisOptions0 = analysisOptions.Value;
        IOpenAIOptions openaiOptions = analysisOptions0.OpenAI;
        IBlobStorageOptions blobStorageOptions = analysisOptions0.BlobStorage;

        this.timeProvider = timeProvider;
        chatClient = new AzureOpenAIClient(openaiOptions.Endpoint, new ApiKeyCredential(openaiOptions.ApiKey))
            .GetChatClient(openaiOptions.Model);
        blobContainerClient = new BlobServiceClient(blobStorageOptions.ConnectionString)
            .GetBlobContainerClient(blobStorageOptions.ContainerPath);

        untitledBlobNameFormat = blobStorageOptions.UntitledBlobNameFormat;
        titledBlobNameFormat = blobStorageOptions.TitledBlobNameFormat;

        makeChatMessagesLazy = new Lazy<Func<IReadOnlyDictionary<string, object?>, IEnumerable<ChatMessage>>>(CalculateMakeChatMessages);
    }

    public Task WriteLogAsync(DateTime timestamp, Guid analysisId, Stream stream, CancellationToken cancellationToken)
    {
        return WriteBlobAsync(timestamp, analysisId, null, LogExtension, stream, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task WriteBlobAsync(
        DateTime timestamp, Guid analysisId, (string Title, string Format)? naming, string extension, Stream stream, CancellationToken cancellationToken
    )
    {
        BlobClient blobClient = await CreateBlobAsync(timestamp, analysisId, naming, extension, cancellationToken);
        await blobClient.UploadAsync(stream, true, cancellationToken);
    }

    private async Task<BlobClient> CreateBlobAsync(
        DateTime timestamp, Guid analysisId, (string Title, string Format)? naming, string extension, CancellationToken cancellationToken
    )
    {
        await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        string blobName = naming is var (title, format)
            ? string.Format(CultureInfo.InvariantCulture, format, timestamp, analysisId, title)
            : string.Format(CultureInfo.InvariantCulture, untitledBlobNameFormat, timestamp, analysisId);
        BlobClient blobClient = blobContainerClient.GetBlobClient($"{blobName}.{extension}");

        IDictionary<string, string> dict = new Dictionary<string, string>()
        {
            [AnalysisIdTagKey] = analysisId.ToString("D"),
            [ExtensionTagKey] = extension,
        };

        await blobClient.SetTagsAsync(dict, cancellationToken: cancellationToken);

        dict[TimestampMetadataKey] = timestamp.ToString("O");
        if (naming is null)
        {
            dict[FormatMetadataKey] = titledBlobNameFormat;
        }

        await blobClient.SetMetadataAsync(dict, cancellationToken: cancellationToken);

        return blobClient;
    }

    public void LabelAnalysis(DateTime? maybeTimestamp, out DateTime timestamp, out Guid analysisId)
    {
        timestamp = maybeTimestamp ?? timeProvider.GetUtcNow().UtcDateTime;
        analysisId = Ulid.NewUlid(timestamp).ToGuid();
    }

    public async Task AnalyzeAsync(
        DateTime timestamp,
        string logContent,
        TextWriter summaryWriter,
        IReadOnlyDictionary<string, object?> placeholderDict,
        CancellationToken cancellationToken
    )
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        placeholderDict = new Dictionary<string, object?>(placeholderDict, StringComparer.OrdinalIgnoreCase)
        {
            ["Timestamp"] = timestamp,
            ["LogContent"] = logContent,
        };
#else
        {
            Dictionary<string, object?> placeholderDict0 = new (StringComparer.OrdinalIgnoreCase)
            {
                ["Timestamp"] = timestamp,
                ["LogContent"] = logContent,
            };
            placeholderDict0.AddRange(placeholderDict);
            placeholderDict = placeholderDict0;
        }
#endif

        IEnumerable<ChatMessage> chatMessages = MakeChatMessages(placeholderDict);
        IAsyncEnumerable<StreamingChatCompletionUpdate> completionUpdates =
            chatClient.CompleteChatStreamingAsync(chatMessages, cancellationToken: cancellationToken);

        await foreach (StreamingChatCompletionUpdate completionUpdate in completionUpdates)
        {
            if (completionUpdate.ContentUpdate is [ var completionContent, .. ])
            {
                await summaryWriter.WriteAsync(completionContent.Text);
            }
        }
    }

    private Func<IReadOnlyDictionary<string, object?>, IEnumerable<ChatMessage>> CalculateMakeChatMessages()
    {
        Deserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(new CamelCaseNamingConvention())
            .Build();

        IEnumerable<YMessage> yMessages;
        using (Stream promptStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(AnalysisService), "Resources.prompt.yaml")!)
        using (TextReader promptReader = new StreamReader(promptStream, Encoding.UTF8))
        {
            yMessages = deserializer.Deserialize<IEnumerable<YMessage>>(promptReader);
        }

        return dict => yMessages.Select(m => ReplacePlaceholders(m, dict)).ToArray();

        static ChatMessage ReplacePlaceholders(YMessage yMessage, IReadOnlyDictionary<string, object?> dict)
        {
            string content = yMessage.Content ?? "";
            if (content == "")
            {
                throw new ArgumentException("Message content is empty");
            }

            while (GoodPlaceholderRegex.Match(content) is { Success: true } placeholderMatch)
            {
                string placeholderName = placeholderMatch.Groups[1].Value;
                if (!dict.TryGetValue(placeholderName, out object? placeholderValue))
                {
                    throw new ArgumentException($"Placeholder value for '{placeholderName}' not available");
                }

                string placeholderFormat = $"{{0{placeholderMatch.Groups[2].Value}{placeholderMatch.Groups[3].Value}}}";
                string placeholderText = string.Format(CultureInfo.InvariantCulture, placeholderFormat, placeholderValue);

                content = $"{content[..placeholderMatch.Index]}{placeholderText}{content[(placeholderMatch.Index + placeholderMatch.Length)..]}";
            }

            if (BadPlaceholderRegex.IsMatch(content))
            {
                throw new ArgumentException("Bad placeholder found");
            }

            content = content.Replace("%%", "%");

            return yMessage.Role switch
            {
                YMessageRole.System => new SystemChatMessage(content),
                YMessageRole.User => new UserChatMessage(content),
                _ => throw new ArgumentOutOfRangeException("Invalid message type", (Exception?)null),
            };
        }
    }

    public Task ConsolidateAsync(Guid analysisId, string title, CancellationToken cancellationToken)
    {
        async Task ConsolidateAsync(string extension)
        {
            BlobClient sourceBlobClient = (await TryGetBlobClientAsync(analysisId, extension, cancellationToken))!;
            IDictionary<string, string> sourceMetadata = (await sourceBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken)).Value.Metadata;

            DateTime timestamp = DateTime.ParseExact(sourceMetadata[TimestampMetadataKey], "O", CultureInfo.InvariantCulture);
            string blobNameFormat = sourceMetadata[FormatMetadataKey];

            using Stream sourceStream = await sourceBlobClient.OpenReadAsync(cancellationToken: cancellationToken);
            await WriteBlobAsync(timestamp, analysisId, (title, blobNameFormat), extension, sourceStream, cancellationToken);
        }

        return Task.WhenAll(ConsolidateAsync(LogExtension), ConsolidateAsync(SummaryExtension));
    }

    public Task<Stream?> TryGetLogStreamAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        return TryGetBlobStreamAsync(analysisId, LogExtension, cancellationToken);
    }

    public Task<Stream?> TryGetSummaryStreamAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        return TryGetBlobStreamAsync(analysisId, SummaryExtension, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<Stream?> TryGetBlobStreamAsync(Guid analysisId, string extension, CancellationToken cancellationToken)
    {
        return await TryGetBlobClientAsync(analysisId, extension, cancellationToken) is { } blobClient
            ? await blobClient.OpenReadAsync(cancellationToken: cancellationToken)
            : null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<BlobClient?> TryGetBlobClientAsync(Guid analysisId, string extension, CancellationToken cancellationToken)
    {
        return await blobContainerClient
            .FindBlobsByTagsAsync($"\"{AnalysisIdTagKey}\"='{analysisId:D}' AND \"{ExtensionTagKey}\"='{extension}'", cancellationToken)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken) is { } blobItem
            ? blobContainerClient.GetBlobClient(blobItem.BlobName)
            : null;
    }

    [UsedImplicitly(ImplicitUseKindFlags.Assign)]
    private sealed class YMessage
    {
        public YMessageRole? Role { get; set; }
        public string? Content { get; set; }
    }

    private enum YMessageRole
    {
        User,
        System,
    }
}
