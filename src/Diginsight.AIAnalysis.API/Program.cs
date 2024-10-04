using Diginsight.AIAnalysis.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Diginsight.AIAnalysis.API;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder applicationBuilder = WebApplication.CreateBuilder(args);

        IConfiguration configuration = applicationBuilder.Configuration;

        IServiceCollection services = applicationBuilder.Services;

        services.AddControllers();

        services
            .Configure<ApiBehaviorOptions>(static o => { o.SuppressMapClientErrors = true; });

        services.AddAIAnalysis(configuration.GetSection("AIAnalysis").Bind);
        services.TryAddScoped<IInnerAnalysisService, InnerAnalysisService>();

        WebApplication application = applicationBuilder.Build();

        application.UseEndpoints(
            static endpoint =>
            {
                endpoint.MapControllers();
            }
        );

        // ReSharper disable once AsyncApostle.AsyncAwaitMayBeElidedHighlighting
        await application.RunAsync();
    }
}
