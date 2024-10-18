using Diginsight.Analyzer.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Repositories.Models;

internal sealed class AnalysisContextDocument
{
    [JsonProperty("id")]
    public string Id { get; }

    public Guid AnalysisId { get; }

    public int Attempt { get; }

    [JsonExtensionData]
    public JObject ExtensionData { get; } = new ();

    [JsonConstructor]
    private AnalysisContextDocument(string id, Guid analysisId, int attempt)
    {
        Id = id;
        AnalysisId = analysisId;
        Attempt = attempt;
    }

    public static AnalysisContextDocument Create(IAnalysisContext analysisContext)
    {
        (Guid analysisId, int attempt) = analysisContext.Coordinate;
        AnalysisContextDocument document = new (analysisContext.PersistenceId.ToString(), analysisId, attempt);

        JsonSerializer serializer = JsonSerializer.CreateDefault();

        JObject rawSource = JObject.FromObject(analysisContext, serializer);
        rawSource.Property(nameof(IAnalysisContext.PersistenceId), StringComparison.OrdinalIgnoreCase)!.Remove();
        rawSource.Property(nameof(IAnalysisContext.Coordinate), StringComparison.OrdinalIgnoreCase)!.Remove();

        using (JsonReader reader = rawSource.CreateReader())
        {
            serializer.Populate(reader, document);
        }

        return document;
    }
}
