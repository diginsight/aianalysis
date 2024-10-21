using Diginsight.Analyzer.Business.Configurations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

public static class BusinessImplExtensions
{
    public static IServiceCollection AddBusiness(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.Any(static x => x.ServiceType == typeof(IExecutionService)))
        {
            return services;
        }

        services
            .AddSingleton<IAmbientService, AmbientService>()
            .AddSingleton<IConfigureOptions<CoreConfig>, ConfigureCoreConfig>()
            .Configure<CoreConfig>(configuration.GetSection("Core"))
            .AddSingleton<ISnapshotService, SnapshotService>()
            .AddSingleton<IReportService, ReportService>()
            .AddSingleton<IInternalMigrationService, InternalMigrationService>()
            .AddScoped(static p => p.GetRequiredService<IParallelismSettingsAccessor>().Get());

        if (CommonUtils.IsAgent)
        {
            services
                .AddSingleton<IAgentExecutionService, AgentExecutionService>()
                .AddSingleton<IExecutionService>(static p => p.GetRequiredService<IAgentExecutionService>())
                .AddSingleton<IAgentLeaseService, AgentLeaseService>()
                .AddSingleton<IAgentAnalysisService, AgentAnalysisService>()
                .AddSingleton<IAnalysisService>(static p => p.GetRequiredService<IAgentAnalysisService>())
                .AddSingleton<IAgentDeletionService, AgentDeletionService>()
                .AddSingleton<IDeletionService>(static p => p.GetRequiredService<IAgentDeletionService>())
                .AddSingleton<IAgentMigrationContextFactory, AgentMigrationContextFactory>()
                .AddSingleton<IAgentParallelismSettingsAccessor, AgentParallelismSettingsAccessor>()
                .AddSingleton<IParallelismSettingsAccessor>(static p => p.GetRequiredService<IAgentParallelismSettingsAccessor>())
                .AddSingleton<IAgentEventMetaAccessor, AgentEventMetaAccessor>()
                .AddSingleton<IEventMetaAccessor>(static p => p.GetRequiredService<IAgentEventMetaAccessor>())
                .AddScoped<IMigrationExecutor, MigrationExecutor>()
                .AddScoped<IDeletionExecutor, DeletionExecutor>()
                .AddSingleton<IFileLoggerFactoryFactory, FileLoggerFactoryFactory>()
                .AddSingleton<IEventService, EventService>();
        }
        else
        {
            services
                .AddSingleton<IOrchestratorExecutionService, OrchestratorExecutionService>()
                .AddSingleton<IExecutionService>(static p => p.GetRequiredService<IOrchestratorExecutionService>())
                .AddSingleton<IOrchestratorLeaseService, OrchestratorLeaseService>()
                .AddSingleton<IOrchestratorAnalysisService, OrchestratorAnalysisService>()
                .AddSingleton<IAnalysisService>(static p => p.GetRequiredService<IOrchestratorAnalysisService>())
                .AddSingleton<IOrchestratorDeletionService, OrchestratorDeletionService>()
                .AddSingleton<IDeletionService>(static p => p.GetRequiredService<IOrchestratorDeletionService>())
                .AddSingleton<IOrchestratorMigrationContextFactory, OrchestratorMigrationContextFactory>()
                .AddSingleton<IParallelismSettingsAccessor, ParallelismSettingsAccessor>()
                .AddSingleton<IEventMetaAccessor, EventMetaAccessor>()
                .AddSingleton<IAgentClientFactory, AgentClientFactory>()
                .AddTransient<AgentDelegatingHandler>()
                .AddSingleton<IDequeuerService, DequeuerService>()
                .AddHostedService(static p => p.GetRequiredService<IDequeuerService>())
                .AddSingleton<IMailerService, MailerService>()
                .AddSingleton<IMailProvider, SendGridMailProvider>()
                .Configure<SendGridConfig>(configuration.GetSection("SendGrid"))
                .AddSingleton<ISendGridClient>(
                    static p =>
                    {
                        ISendGridConfig config = p.GetRequiredService<IOptions<SendGridConfig>>().Value;
                        return new SendGridClient(config.ApiKey);
                    }
                )
                .AddHttpClient(typeof(AgentClient).FullName!)
                .AddHttpMessageHandler<AgentDelegatingHandler>()
                .ConfigureHttpClient(
                    static (sp, hc) =>
                    {
                        IOrchestratorCoreConfig coreConfig = sp.GetRequiredService<IOptionsMonitor<CoreConfig>>().CurrentValue;
                        hc.Timeout = TimeSpan.FromSeconds(coreConfig.AgentTimeoutSeconds);
                    }
                );
        }

        return services;
    }

    public static void RegisterBusinessLifetimeEvents(this IServiceProvider serviceProvider)
    {
        IHostApplicationLifetime applicationLifetime = serviceProvider.GetRequiredService<IHostApplicationLifetime>();

        if (serviceProvider.GetService<IAgentExecutionService>() is { } agentExecutionService)
        {
            applicationLifetime.ApplicationStopping.Register(
                () => { agentExecutionService.WaitForFinishAsync().GetAwaiter().GetResult(); }
            );
        }

        if (serviceProvider.GetService<IAgentLeaseService>() is { } agentLeaseService)
        {
            applicationLifetime.ApplicationStarted.Register(
                () => { agentLeaseService.CreateAsync().GetAwaiter().GetResult(); }
            );
            applicationLifetime.ApplicationStopping.Register(
                () => { agentLeaseService.DeleteAsync().GetAwaiter().GetResult(); }
            );
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LoggerConfiguration CustomizedWriteTo(
        this LoggerConfiguration loggerConfiguration,
        Action<LoggerSinkConfiguration> configure,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Verbose,
        LoggingLevelSwitch? levelSwitch = null
    )
    {
        return LoggerSinkConfiguration.Wrap(
            loggerConfiguration.WriteTo,
            static s => new CustomizedSink(s),
            configure,
            restrictedToMinimumLevel,
            levelSwitch
        );
    }

    public static LoggerConfiguration BasicWriteToFiles(this LoggerConfiguration loggerConfiguration, string fileName, params string[] pathSegments)
    {
        BasicWriteToFile(BusinessUtils.MakeExpressionTemplate(false), "log");
        BasicWriteToFile(new RenderedCompactJsonFormatter(), "json");
        return loggerConfiguration;

        void BasicWriteToFile(ITextFormatter textFormatter, string extension)
        {
            loggerConfiguration.CustomizedWriteTo(
                writeTo => writeTo.File(
                    textFormatter,
                    Path.Combine(pathSegments.Prepend("logs").Append($"{fileName}.{extension}").ToArray()),
                    fileSizeLimitBytes: null
                )
            );
        }
    }

    private sealed class ConfigureCoreConfig : IConfigureOptions<CoreConfig>
    {
        private readonly IHostEnvironment hostEnvironment;

        public ConfigureCoreConfig(IHostEnvironment hostEnvironment)
        {
            this.hostEnvironment = hostEnvironment;
        }

        public void Configure(CoreConfig coreConfig)
        {
            if (hostEnvironment.IsLocal())
            {
                coreConfig.DefaultParallelism = 1;
                coreConfig.DefaultFamily = $"local_{Environment.MachineName}";
            }
        }
    }

    private sealed class CustomizedSink : ILogEventSink, IDisposable
    {
        private readonly ILogEventSink decoratee;

        public CustomizedSink(ILogEventSink decoratee)
        {
            this.decoratee = decoratee;
        }

        public void Emit(LogEvent logEvent)
        {
            if (logEvent.Level == LogEventLevel.Information &&
                logEvent.Properties.GetValueOrDefault("SourceContext") is ScalarValue { Value: string sourceContext } &&
                !(sourceContext.StartsWith("ABB.Ability.EL.Common.Clients.") || sourceContext.StartsWith("ABB.Ability.EL.DataMigration.")))
            {
                logEvent = new LogEvent(
                    logEvent.Timestamp,
                    LogEventLevel.Debug,
                    logEvent.Exception,
                    logEvent.MessageTemplate,
                    logEvent.Properties.Select(static x => new LogEventProperty(x.Key, x.Value))
                );
            }

            decoratee.Emit(logEvent);
        }

        public void Dispose()
        {
            (decoratee as IDisposable)?.Dispose();
        }
    }
}
