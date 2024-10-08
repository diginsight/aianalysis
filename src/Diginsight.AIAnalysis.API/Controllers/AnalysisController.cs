using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Mime;
using System.Text;

namespace Diginsight.AIAnalysis.API.Controllers;

[Route("analysis")]
public class AnalysisController : ControllerBase
{
    private const string? LogFormPartName = "log";
    private const string? PlaceholdersFormPartName = "placeholders";

    private static readonly JsonSerializer JsonSerializer = JsonSerializer.CreateDefault(
        new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Auto }
    );

    private readonly IAnalysisService analysisService;

    public AnalysisController(
        IAnalysisService analysisService
    )
    {
        this.analysisService = analysisService;
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    [Produces(MediaTypeNames.Text.Plain, Type = typeof(Guid))]
    public async Task<IActionResult> Analyze(
        [FromForm(Name = LogFormPartName)] IFormFile? logFile,
        [FromForm(Name = PlaceholdersFormPartName)] IFormFile? placeholdersFile,
        [FromQuery] DateTime? timestamp
    )
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;

        if (logFile is null)
        {
            return Problem($"Part '{LogFormPartName}' missing", statusCode: StatusCodes.Status400BadRequest);
        }
        if (placeholdersFile is null)
        {
            return Problem($"Part '{PlaceholdersFormPartName}' missing", statusCode: StatusCodes.Status400BadRequest);
        }

        if (!MediaTypeHeaderValue.TryParse(logFile.ContentType, out MediaTypeHeaderValue? logMediaType)
            || logMediaType.MediaType != MediaTypeNames.Text.Plain)
        {
            return Problem(
                $"Media type of part '{LogFormPartName}' is not {MediaTypeNames.Text.Plain}",
                statusCode: StatusCodes.Status415UnsupportedMediaType
            );
        }
        if (!MediaTypeHeaderValue.TryParse(placeholdersFile.ContentType, out MediaTypeHeaderValue? placeholdersMediaType)
            || placeholdersMediaType.MediaType != MediaTypeNames.Application.Json)
        {
            return Problem(
                $"Media type of part '{PlaceholdersFormPartName}' is not {MediaTypeNames.Application.Json}",
                statusCode: StatusCodes.Status415UnsupportedMediaType
            );
        }

        IReadOnlyDictionary<string, object?> placeholders;
        {
            Encoding placeholdersEncoding = placeholdersMediaType.Encoding ?? Encoding.UTF8;
            JObject rawPlaceholders;
            await using (Stream placeholdersStream = placeholdersFile.OpenReadStream())
            using (TextReader placeholdersTextReader = new StreamReader(placeholdersStream, placeholdersEncoding))
            await using (JsonReader placeholdersJsonReader = new JsonTextReader(placeholdersTextReader))
            {
                rawPlaceholders = await JObject.LoadAsync(placeholdersJsonReader, cancellationToken);
            }

            placeholders = ((IDictionary<string, JToken>)rawPlaceholders)
                .ToDictionary(static x => x.Key, static x => x.Value.ToObject<object?>(JsonSerializer), StringComparer.OrdinalIgnoreCase);
        }

        IPartialAnalysisResult partialAnalysisResult;
        await using (Stream logStream = logFile.OpenReadStream())
        {
            Encoding logEncoding = logMediaType.Encoding ?? Encoding.UTF8;
            partialAnalysisResult = await analysisService.StartAnalyzeAsync(logStream, logEncoding, placeholders, timestamp, cancellationToken);
        }

        return new OkObjectResult(partialAnalysisResult.Id.ToString("D")) { ContentTypes = { MediaTypeNames.Text.Plain } };
    }

    [HttpGet]
    [Route("{analysisId:guid}/title")]
    [Produces(MediaTypeNames.Text.Plain)]
    public async Task<IActionResult> GetTitle([FromRoute] Guid analysisId)
    {
        return await analysisService.TryGetTitleAsync(analysisId, HttpContext.RequestAborted) is { } title
            ? new OkObjectResult(title) { ContentTypes = { MediaTypeNames.Text.Plain } }
            : new NotFoundResult();
    }

    [HttpGet]
    [Route("{analysisId:guid}/log")]
    [Produces(MediaTypeNames.Text.Plain)]
    public Task<IActionResult> GetLog([FromRoute] Guid analysisId)
    {
        return GetStreamAndEncodingAsync(analysisId, analysisService.TryGetLogAsync, MediaTypeNames.Text.Plain, "log");
    }

    [HttpGet]
    [Route("{analysisId:guid}/summary")]
    [Produces(MediaTypeNames.Text.Html)]
    public Task<IActionResult> GetSummary([FromRoute] Guid analysisId)
    {
        return GetStreamAndEncodingAsync(analysisId, analysisService.TryGetSummaryAsync, MediaTypeNames.Text.Html, "html");
    }

    private async Task<IActionResult> GetStreamAndEncodingAsync(
        Guid analysisId,
        Func<Guid, CancellationToken, Task<(Stream, Encoding)?>> coreGetStreamAsync,
        string mediaType,
        string extension
    )
    {
        if (await coreGetStreamAsync(analysisId, HttpContext.RequestAborted) is not var (stream, encoding))
        {
            return new NotFoundResult();
        }

        return new FileStreamResult(stream, new MediaTypeHeaderValue(mediaType) { Charset = encoding.WebName })
        {
            FileDownloadName = Request.Query.ContainsKey("download") ? $"{analysisId:N}.{extension}" : null,
        };
    }
}
