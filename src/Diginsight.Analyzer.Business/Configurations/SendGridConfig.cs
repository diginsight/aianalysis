namespace Diginsight.Analyzer.Business.Configurations;

internal sealed class SendGridConfig : ISendGridConfig
{
    public string ApiKey { get; set; } = null!;
}
