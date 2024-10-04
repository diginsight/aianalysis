namespace Diginsight.AIAnalysis;

public interface IAnalysisOptions
{
    IOpenAIOptions OpenAI { get; }
    IBlobStorageOptions BlobStorage { get; }
}
