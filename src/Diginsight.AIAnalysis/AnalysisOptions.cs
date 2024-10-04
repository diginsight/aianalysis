using JetBrains.Annotations;

namespace Diginsight.AIAnalysis;

public sealed class AnalysisOptions : IAnalysisOptions
{
    [PublicAPI]
    public OpenAIOptions OpenAI { get; } = new ();

    IOpenAIOptions IAnalysisOptions.OpenAI => OpenAI;

    [PublicAPI]
    public BlobStorageOptions BlobStorage { get; } = new ();

    IBlobStorageOptions IAnalysisOptions.BlobStorage => BlobStorage;
}
