using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;

namespace Diginsight.AIAnalysis.AspNetCore;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class WebApplicationExtensions
{
    [PublicAPI]
    public static IServiceCollection AddAIAnalysisWeb(this IServiceCollection services, Action<AnalysisOptions>? configureAnalysisOptions = null)
    {
        return services
            .AddLoggerFactorySetter()
            .AddAIAnalysis(configureAnalysisOptions);
    }
}
