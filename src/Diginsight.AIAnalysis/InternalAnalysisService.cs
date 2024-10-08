using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using JetBrains.Annotations;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Mime;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace Diginsight.AIAnalysis;

internal sealed class InternalAnalysisService : IInternalAnalysisService
{
    private const string AnalysisIdTagKey = "aid";
    private const string ExtensionTagKey = "ext";
    private const string TimestampMetadataKey = "ts";
    private const string TitleMetadataKey = "tit";
    private const string FormatMetadataKey = "fmt";

    private const string LogExtension = "log";
    private const string SummaryExtension = "html";

    private static readonly Regex GoodPlaceholderRegex = new ("%([^,:%]+?)(,\\d+?)?(:[^%]+?)?%");
    private static readonly Regex BadPlaceholderRegex = new ("%([^%]+?)%");

    private static readonly Encoding BlobEncoding = Encoding.UTF8;

    private readonly TimeProvider timeProvider;
    private readonly ChatClient chatClient;
    private readonly BlobContainerClient blobContainerClient;
    private readonly string untitledBlobNameFormat;
    private readonly string titledBlobNameFormat;

    private IList<IEnumerable<YMessage>>? yMessageGroups;

    private IList<IEnumerable<YMessage>> YMessageGroups => LazyInitializer.EnsureInitialized(ref yMessageGroups, MakeYMessageGroups)!;

    public InternalAnalysisService(
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
    }

    private static IList<IEnumerable<YMessage>> MakeYMessageGroups()
    {
        Deserializer deserializer = new DeserializerBuilder().Build();

        using Stream promptStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(InternalAnalysisService), "Resources.prompt.yaml")!;
        using TextReader promptReader = new StreamReader(promptStream, Encoding.UTF8);

        Parser parser = new (promptReader);
        parser.Expect<StreamStart>();

        IList<IEnumerable<YMessage>> yMessageGroups = new List<IEnumerable<YMessage>>();
        while (parser.Accept<DocumentStart>())
        {
            yMessageGroups.Add(deserializer.Deserialize<IEnumerable<YMessage>>(parser));
        }

        return yMessageGroups;
    }

    public void LabelAnalysis(DateTime? maybeTimestamp, out DateTime timestamp, out Guid analysisId)
    {
        timestamp = maybeTimestamp ?? timeProvider.GetUtcNow().UtcDateTime;
        analysisId = Ulid.NewUlid(timestamp).ToGuid();
    }

    public async Task WriteLogAsync(DateTime timestamp, Guid analysisId, Stream stream, CancellationToken cancellationToken)
    {
        _ = await CreateBlobAsync(timestamp, analysisId, null, LogExtension, MediaTypeNames.Text.Plain, stream, cancellationToken);
    }

    private async Task<AppendBlobClient> CreateBlobAsync(
        DateTime timestamp,
        Guid analysisId,
        (string Title, string BlobName)? naming,
        string contentType,
        string extension,
        Stream? stream,
        CancellationToken cancellationToken
    )
    {
        await blobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        string finalBlobName = naming?.BlobName ?? string.Format(CultureInfo.InvariantCulture, untitledBlobNameFormat, timestamp, analysisId);
        AppendBlobClient blobClient = blobContainerClient.GetAppendBlobClient($"{finalBlobName}.{extension}");

        IDictionary<string, string> blobMetadata = new Dictionary<string, string>()
        {
            [TimestampMetadataKey] = timestamp.ToString("O"),
        };
        if (naming is var (title, _))
        {
            blobMetadata[TitleMetadataKey] = title;
        }
        else
        {
            blobMetadata[FormatMetadataKey] = titledBlobNameFormat;
        }

        await blobClient.CreateAsync(
            new AppendBlobCreateOptions()
            {
                Tags = new Dictionary<string, string>()
                {
                    [AnalysisIdTagKey] = analysisId.ToString("D"),
                    [ExtensionTagKey] = extension,
                },
                Metadata = blobMetadata,
                HttpHeaders = new BlobHttpHeaders()
                {
                    ContentType = contentType,
                    ContentEncoding = BlobEncoding.WebName,
                }
            },
            cancellationToken
        );

        if (stream is not null)
        {
            await blobClient.AppendBlockAsync(stream, cancellationToken: cancellationToken);
            await blobClient.SealAsync(cancellationToken: cancellationToken);
        }

        return blobClient;
    }

    public async Task<string> AnalyzeAsync(
        DateTime timestamp,
        Guid analysisId,
        string logContent,
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

        IEnumerable<ChatMessage> turn0Messages = MakeChatMessages(0, placeholderDict);
        IAsyncEnumerable<StreamingChatCompletionUpdate> turn0CompletionUpdates =
            chatClient.CompleteChatStreamingAsync(turn0Messages, cancellationToken: cancellationToken);

        AppendBlobClient summaryBlobClient =
            await CreateBlobAsync(timestamp, analysisId, null, SummaryExtension, MediaTypeNames.Text.Html, null, cancellationToken);

        ICollection<ChatMessageContentPart> turn0ReplyMessageParts = new List<ChatMessageContentPart>();
#if NET || NETSTANDARD2_1_OR_GREATER
        await using
#else
        using
#endif
            (Stream summaryStream = await summaryBlobClient.OpenWriteAsync(false, cancellationToken: cancellationToken))
#if NET || NETSTANDARD2_1_OR_GREATER
        await using
#else
        using
#endif
            (TextWriter summaryWriter = new StreamWriter(summaryStream, BlobEncoding))
        {
            await foreach (StreamingChatCompletionUpdate completionUpdate in turn0CompletionUpdates)
            {
                if (completionUpdate.ContentUpdate is [ var replyMessagePart, .. ])
                {
                    turn0ReplyMessageParts.Add(replyMessagePart);
                    await summaryWriter.WriteAsync(replyMessagePart.Text);
                }
            }
        }

        IEnumerable<ChatMessage> turn1Messages = turn0Messages
            .Append(new AssistantChatMessage(turn0ReplyMessageParts))
            .Concat(MakeChatMessages(1, placeholderDict));
        IAsyncEnumerable<StreamingChatCompletionUpdate> turn1CompletionUpdates =
            chatClient.CompleteChatStreamingAsync(turn1Messages, cancellationToken: cancellationToken);

        string title;
#if NET || NETSTANDARD2_1_OR_GREATER
        await using
#else
        using
#endif
            (StringWriter titleWriter = new ())
        {
            await foreach (StreamingChatCompletionUpdate completionUpdate in turn1CompletionUpdates)
            {
                if (completionUpdate.ContentUpdate is [ var replyChatMessagePart, .. ])
                {
                    await titleWriter.WriteAsync(replyChatMessagePart.Text);
                }
            }

            title = titleWriter.ToString();
        }

        return title;
    }

    private IEnumerable<ChatMessage> MakeChatMessages(int turn, IReadOnlyDictionary<string, object?> placeholderDict)
    {
        return YMessageGroups[turn].Select(m => ReplacePlaceholders(m, placeholderDict)).ToArray();

        static ChatMessage ReplacePlaceholders(YMessage yMessage, IReadOnlyDictionary<string, object?> dict)
        {
            string content = yMessage.Content;

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
        async Task CoreConsolidateAsync(string extension, string contentType)
        {
            BlobClient sourceBlobClient = (await TryGetBlobClientAsync(analysisId, extension, cancellationToken))!;
            IDictionary<string, string> sourceMetadata = (await sourceBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken)).Value.Metadata;

            DateTime timestamp = DateTime.ParseExact(sourceMetadata[TimestampMetadataKey], "O", CultureInfo.InvariantCulture);
            string blobNameFormat = sourceMetadata[FormatMetadataKey];

            string targetBlobName = string.Format(CultureInfo.InvariantCulture, blobNameFormat, timestamp, analysisId, title);
            if ($"{targetBlobName}.{extension}" != sourceBlobClient.Name)
            {
#if NET || NETSTANDARD2_1_OR_GREATER
                await using
#else
                using
#endif
                    Stream sourceStream = await sourceBlobClient.OpenReadAsync(cancellationToken: cancellationToken);
                _ = await CreateBlobAsync(timestamp, analysisId, (title, targetBlobName), extension, contentType, sourceStream, cancellationToken);
            }
            else
            {
                sourceMetadata[TitleMetadataKey] = title;
                await sourceBlobClient.SetMetadataAsync(sourceMetadata, cancellationToken: cancellationToken);
            }
        }

        return Task.WhenAll(
            CoreConsolidateAsync(LogExtension, MediaTypeNames.Text.Plain),
            CoreConsolidateAsync(SummaryExtension, MediaTypeNames.Text.Html)
        );
    }

    public async Task<string?> TryGetTitleAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        return await TryGetBlobClientAsync(analysisId, LogExtension, cancellationToken) is { } blobClient
            && (await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken)).Value.Metadata.TryGetValue(TitleMetadataKey, out string? title)
                ? title
                : null;
    }

    public async Task<(Stream Stream, Encoding Encoding)?> TryGetLogAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        return await TryGetBlobStreamAsync(analysisId, LogExtension, cancellationToken) is { } stream
            ? (stream, BlobEncoding) : null;
    }

    public async Task<(Stream Stream, Encoding Encoding)?> TryGetSummaryAsync(Guid analysisId, CancellationToken cancellationToken)
    {
        return await TryGetBlobStreamAsync(analysisId, SummaryExtension, cancellationToken) is { } stream
            ? (stream, BlobEncoding) : null;
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
        private string? content;

        public YMessageRole? Role { get; set; }

        [AllowNull]
        public string Content
        {
            get => content ?? throw new InvalidOperationException($"{nameof(Content)} is unset");
            set => content = string.IsNullOrEmpty(value) ? throw new ArgumentNullException(nameof(Content)) : value;
        }
    }

    private enum YMessageRole
    {
        User,
        System,
    }
}
