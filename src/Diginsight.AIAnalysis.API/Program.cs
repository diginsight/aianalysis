using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Console;
using ILoggerProvider = Microsoft.Extensions.Logging.ILoggerProvider;

namespace Diginsight.AIAnalysis.API;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        WebApplicationBuilder applicationBuilder = WebApplication.CreateBuilder(args);

        IConfiguration configuration = applicationBuilder.Configuration;

        IServiceCollection services = applicationBuilder.Services;

        foreach (ServiceDescriptor sd in services.Where(x => x.ServiceType == typeof(ILoggerProvider)).ToArray())
        {
            if (sd.ImplementationType == typeof(ConsoleLoggerProvider))
                continue;

            services.Remove(sd);
        }

        services.AddControllers();

        services
            .Configure<ApiBehaviorOptions>(static o => { o.SuppressMapClientErrors = true; });

        services.AddAIAnalysis(configuration.GetSection("Analysis").Bind);

        WebApplication application = applicationBuilder.Build();

        application.UseRouting();

        application.UseEndpoints(
            static endpoint => { endpoint.MapControllers(); }
        );

        // ReSharper disable once AsyncApostle.AsyncAwaitMayBeElidedHighlighting
        await application.RunAsync();
    }
}
