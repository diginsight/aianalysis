namespace Diginsight.AIAnalysis;

public interface IOpenAIOptions
{
    Uri Endpoint { get; }
    string ApiKey { get; }
    string Model { get; }
}
