using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.ComponentModel;

namespace Diginsight.AIAnalysis;

[EditorBrowsable(EditorBrowsableState.Never)]
public static class ServiceCollectionExtensions
{
    [PublicAPI]
    public static IServiceCollection AddAIAnalysis(this IServiceCollection services, Action<AnalysisOptions>? configureAnalysisOptions = null)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IAnalysisService, AnalysisService>();

        if (configureAnalysisOptions is not null)
        {
            services.Configure(configureAnalysisOptions);
        }

        return services;
    }
}
