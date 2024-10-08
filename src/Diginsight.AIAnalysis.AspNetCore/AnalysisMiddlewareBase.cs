using Diginsight.Diagnostics.TextWriting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace Diginsight.AIAnalysis.AspNetCore;

public abstract class AnalysisMiddlewareBase : IMiddleware
{
    private readonly IAnalysisService analysisService;
    private readonly ILoggerFactorySetter loggerFactorySetter;
    private readonly TimeProvider timeProvider;

    protected abstract IReadOnlyDictionary<string, object?> Placeholders { get; }

    protected AnalysisMiddlewareBase(
        IAnalysisService analysisService,
        ILoggerFactorySetter loggerFactorySetter,
        TimeProvider? timeProvider = null
    )
    {
        this.analysisService = analysisService;
        this.loggerFactorySetter = loggerFactorySetter;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!Convert.ToBoolean(context.Request.Headers["AIAnalysis"].LastOrDefault()))
        {
            await next(context);
            return;
        }

        Activity? activity = Activity.Current;
        DateTime? timestamp = activity?.StartTimeUtc;
        CancellationToken cancellationToken = context.RequestAborted;

        IPartialAnalysisResult result;
        using (MemoryStream logStream = new ())
        {
            Encoding logEncoding = Encoding.UTF8;

#if NET || NETSTANDARD2_1_OR_GREATER
            await using
#else
            using
#endif
            (TextWriter logWriter =
#if NET
                new StreamWriter(logStream, logEncoding, leaveOpen: true)
#else
                new StreamWriter(logStream, logEncoding, 1024, true)
#endif
            )
            {
                IEnumerable<ILoggerProvider> localLoggerProviders = [ new TextWriterLoggerProvider(timeProvider, logWriter) ];

                using (ILoggerFactory localLoggerFactory = ActivatorUtilities.CreateInstance<LoggerFactory>(context.RequestServices, localLoggerProviders))
                using (ILoggerFactory compositeLoggerFactory = new CompositeLoggerFactory(loggerFactorySetter, localLoggerFactory))
                using (loggerFactorySetter.WithLoggerFactory(compositeLoggerFactory))
                {
                    await next(context);
                }
            }

            logStream.Position = 0;

            result = await analysisService.StartAnalyzeAsync(logStream, logEncoding, Placeholders, timestamp, cancellationToken);
        }

        context.Response.Headers["AIAnalysis-Id"] = result.Id.ToString("D");
        context.Response.Headers["AIAnalysis-Timestamp"] = result.Timestamp.ToString("O");
    }

    private sealed class CompositeLoggerFactory : ILoggerFactory
    {
        private readonly ILoggerFactory primary;
        private readonly ILoggerFactory local;

        private readonly ConcurrentDictionary<string, ILogger> cachedLoggers = new ();

        public CompositeLoggerFactory(ILoggerFactory primary, ILoggerFactory local)
        {
            this.primary = primary;
            this.local = local;
        }

        public void AddProvider(ILoggerProvider provider) => primary.AddProvider(provider);

        public ILogger CreateLogger(string categoryName) =>
            cachedLoggers.GetOrAdd(categoryName, x => new Logger(primary.CreateLogger(x), local.CreateLogger(x)));

        private sealed class Logger : ILogger
        {
            private readonly ILogger primary;
            private readonly ILogger local;

            public Logger(ILogger primary, ILogger local)
            {
                this.primary = primary;
                this.local = local;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                primary.Log(logLevel, eventId, state, exception, formatter);
                local.Log(logLevel, eventId, state, exception, formatter);
            }

            public bool IsEnabled(LogLevel logLevel) => primary.IsEnabled(logLevel) || local.IsEnabled(logLevel);

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => primary.BeginScope(state);
        }

        public void Dispose() { }
    }

    private sealed class TextWriterLoggerProvider : ILoggerProvider
    {
        private readonly TimeProvider timeProvider;
        private readonly TextWriter textWriter;

        private readonly ConcurrentDictionary<string, ILogger> cachedLoggers = new ();

        public TextWriterLoggerProvider(
            TimeProvider timeProvider,
            TextWriter textWriter
        )
        {
            this.timeProvider = timeProvider;
            this.textWriter = textWriter;
        }

        public ILogger CreateLogger(string categoryName) =>
            cachedLoggers.GetOrAdd(categoryName, x => new Logger(timeProvider, textWriter, x));

        private sealed class Logger : ILogger
        {
            private readonly TimeProvider timeProvider;
            private readonly TextWriter textWriter;
            private readonly string categoryName;

            public Logger(
                TimeProvider timeProvider,
                TextWriter textWriter,
                string categoryName
            )
            {
                this.timeProvider = timeProvider;
                this.textWriter = textWriter;
                this.categoryName = categoryName;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                object? finalState = state;
                DiginsightTextWriter.ExpandState(
                    ref finalState,
                    out bool isActivity,
                    out TimeSpan? duration,
                    out DateTimeOffset? maybeTimestamp,
                    out Activity? activity,
                    out Func<LineDescriptor, LineDescriptor>? sealLineDescriptor
                );

                DiginsightTextWriter.Write(
                    textWriter,
                    false,
                    (maybeTimestamp ?? timeProvider.GetUtcNow()).UtcDateTime,
                    activity,
                    logLevel,
                    categoryName,
                    formatter(state, exception),
                    exception,
                    isActivity,
                    duration,
                    LineDescriptor.DefaultDescriptor,
                    sealLineDescriptor
                );
            }

            public bool IsEnabled(LogLevel logLevel) => true;

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        }

        public void Dispose() { }
    }
}
