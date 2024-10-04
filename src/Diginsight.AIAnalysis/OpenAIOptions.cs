using JetBrains.Annotations;

namespace Diginsight.AIAnalysis;

public sealed class OpenAIOptions : IOpenAIOptions
{
    [PublicAPI]
    public string? Endpoint { get; set; }

    Uri IOpenAIOptions.Endpoint => Endpoint is { } endpoint ? new Uri(endpoint) : throw new InvalidOperationException($"{nameof(Endpoint)} is unset");

    [PublicAPI]
    public string? ApiKey { get; set; }

    string IOpenAIOptions.ApiKey => ApiKey ?? throw new InvalidOperationException($"{nameof(ApiKey)} is unset");

    [PublicAPI]
    public string? Model { get; set; } = "gpt-4o";

    string IOpenAIOptions.Model => Model ?? throw new InvalidOperationException($"{nameof(Model)} is unset");
}
