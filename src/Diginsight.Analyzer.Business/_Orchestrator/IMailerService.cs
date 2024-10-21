using Newtonsoft.Json.Linq;

namespace Diginsight.Analyzer.Business;

public interface IMailerService
{
    Task SendIfNeededAsync(JObject rawEvent);
}
