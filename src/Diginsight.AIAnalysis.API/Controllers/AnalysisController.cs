using Diginsight.AIAnalysis.API.Services;
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
    private static readonly JsonSerializer JsonSerializer = JsonSerializer.CreateDefault(
        new JsonSerializerSettings() { TypeNameHandling = TypeNameHandling.Auto }
    );

    private readonly IAnalysisService analysisService;
    private readonly IServiceScopeFactory serviceScopeFactory;

    public AnalysisController(
        IAnalysisService analysisService,
        IServiceScopeFactory serviceScopeFactory
    )
    {
        this.analysisService = analysisService;
        this.serviceScopeFactory = serviceScopeFactory;
    }

    [HttpPost]
    public async Task<IActionResult> Analyze(
        [FromForm(Name = "log")] IFormFile logFile,
        [FromForm(Name = "placeholders")] IFormFile placeholdersFile,
        [FromQuery] DateTime? timestamp
    )
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;

        if (!MediaTypeHeaderValue.TryParse(logFile.ContentType, out MediaTypeHeaderValue? logMediaType)
            || logMediaType.MediaType != MediaTypeNames.Text.Plain)
        {
            return new UnsupportedMediaTypeResult();
        }
        if (!MediaTypeHeaderValue.TryParse(placeholdersFile.ContentType, out MediaTypeHeaderValue? placeholdersMediaType)
            || placeholdersMediaType.MediaType != MediaTypeNames.Application.Json)
        {
            return new UnsupportedMediaTypeResult();
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

        analysisService.LabelAnalysis(timestamp, out DateTime finalTimestamp, out Guid analysisId);

        string logContent;
        using (MemoryStream tempLogStream = new ())
        {
            await using (Stream logStream = logFile.OpenReadStream())
            {
                await logStream.CopyToAsync(tempLogStream, cancellationToken);
            }

            tempLogStream.Position = 0;
            Encoding logEncoding = logMediaType.Encoding ?? Encoding.UTF8;
            using (TextReader logTextReader = new StreamReader(tempLogStream, logEncoding))
            {
                logContent = await logTextReader.ReadToEndAsync(cancellationToken);
            }

            tempLogStream.Position = 0;
            await analysisService.WriteLogAsync(finalTimestamp, analysisId, tempLogStream, cancellationToken);
        }

        TaskUtils.RunAndForget(
            async () =>
            {
                using IServiceScope serviceScope = serviceScopeFactory.CreateScope();
                IServiceProvider serviceProvider = serviceScope.ServiceProvider;
                IInnerAnalysisService innerAnalysisService = serviceProvider.GetRequiredService<IInnerAnalysisService>();

                await innerAnalysisService.AnalyzeAsync(finalTimestamp, analysisId, logContent, placeholders);
            },
            cancellationToken
        );

        return new OkObjectResult(analysisId.ToString("D")) { ContentTypes = { MediaTypeNames.Text.Plain } };
    }

    [HttpGet]
    [Route("{analysisId:guid}/summary")]
    public async Task<IActionResult> GetSummary([FromRoute] Guid analysisId)
    {
        Stream? summaryStream = await analysisService.TryGetSummaryStreamAsync(analysisId);
        if (summaryStream is null)
        {
            return new NotFoundResult();
        }

        return new FileStreamResult(summaryStream, MediaTypeNames.Text.Html)
        {
            FileDownloadName = Request.Query.ContainsKey("download") ? $"{analysisId:N}.html" : null,
        };
    }
}
