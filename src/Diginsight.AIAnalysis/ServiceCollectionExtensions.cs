using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
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
        services.TryAddSingleton<IInternalAnalysisService, InternalAnalysisService>();

        if (configureAnalysisOptions is not null)
        {
            services.Configure(configureAnalysisOptions);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AnalysisOptions>, ValidateAnalysisOptions>());

        return services;
    }

    private sealed class ValidateAnalysisOptions : IValidateOptions<AnalysisOptions>
    {
        public ValidateOptionsResult Validate(string? name, AnalysisOptions options)
        {
            if (name != Options.DefaultName)
            {
                return ValidateOptionsResult.Skip;
            }

            ICollection<string> failures = new List<string>();

#if NET || NETSTANDARD2_1_OR_GREATER
            if (options.BlobStorage.UntitledBlobNameFormat?.Contains("{1}", StringComparison.Ordinal) == false)
#else
            if (options.BlobStorage.UntitledBlobNameFormat?.Contains("{1}") == false)
#endif
            {
                failures.Add($"{nameof(AnalysisOptions.BlobStorage)}.{nameof(BlobStorageOptions.UntitledBlobNameFormat)} does not contain mandatory placeholder {{1}}");
            }
#if NET || NETSTANDARD2_1_OR_GREATER
            if (options.BlobStorage.TitledBlobNameFormat?.Contains("{1}", StringComparison.Ordinal) == false)
#else
            if (options.BlobStorage.TitledBlobNameFormat?.Contains("{1}") == false)
#endif
            {
                failures.Add($"{nameof(AnalysisOptions.BlobStorage)}.{nameof(BlobStorageOptions.TitledBlobNameFormat)} does not contain mandatory placeholder {{1}}");
            }

            return failures.Any() ? ValidateOptionsResult.Fail(failures) : ValidateOptionsResult.Success;
        }
    }
}
