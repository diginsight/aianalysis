using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using IMSLogger = Microsoft.Extensions.Logging.ILogger;
using ISerilogLogger = Serilog.ILogger;

namespace Diginsight.Analyzer.Business;

internal class FileLoggerFactoryFactory : IFileLoggerFactoryFactory
{
    private readonly ISerilogLogger logger;

    public FileLoggerFactoryFactory(ISerilogLogger logger)
    {
        this.logger = logger;
    }

    public ILoggerFactory MakeGlobal(DateTime startedAt, Guid instanceId)
    {
        return new FileLoggerFactory(
            new LogScopeBuilder().With("InstanceId", instanceId),
            logger,
            MakeBasicFileLogger(startedAt)
        );
    }

    public ILoggerFactory MakeSite(DateTime startedAt, Guid instanceId, Guid siteId)
    {
        return new FileLoggerFactory(
            new LogScopeBuilder().With("InstanceId", instanceId).With("SiteId", siteId),
            logger,
            MakeBasicFileLogger(startedAt, siteId.ToString())
        );
    }

    private static ISerilogLogger MakeBasicFileLogger(DateTime startedAt, params string[] pathSegments)
    {
        return BusinessUtils.NewLoggerConfiguration()
            .MinimumLevel.Verbose()
            .BasicWriteToFiles(CommonUtils.GetGlobalFileName(startedAt), pathSegments)
            .CreateLogger();
    }

    private sealed class FileLoggerFactory : ILoggerFactory
    {
        private readonly LogScopeBuilder logScopeBuilder;
        private readonly ICollection<ILoggerProvider> loggerProviders;

        private readonly IDictionary<string, (IMSLogger Logger, IDisposable? Scope)> cache = new Dictionary<string, (IMSLogger, IDisposable?)>();
        private readonly object @lock = new ();

        private bool disposed = false;

        public FileLoggerFactory(LogScopeBuilder logScopeBuilder, params ISerilogLogger[] basicLoggers)
        {
            this.logScopeBuilder = logScopeBuilder;
            loggerProviders = basicLoggers.Select(static x => new SerilogLoggerProvider(x, true)).ToList<ILoggerProvider>();
        }

        public IMSLogger CreateLogger(string categoryName)
        {
            CheckDisposed();

            lock (@lock)
            {
                CheckDisposed();

                IMSLogger logger;
                if (cache.TryGetValue(categoryName, out (IMSLogger Logger, IDisposable?) entry))
                {
                    logger = entry.Logger;
                }
                else
                {
                    logger = new MulticastLogger(loggerProviders.Select(x => x.CreateLogger(categoryName)).ToArray());
                    IDisposable? scope = logScopeBuilder.Begin(logger);
                    cache[categoryName] = (logger, scope);
                }

                return logger;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void CheckDisposed()
            {
                if (disposed)
                {
                    throw new ObjectDisposedException(nameof(FileLoggerFactory));
                }
            }
        }

        public void AddProvider(ILoggerProvider provider)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            lock (@lock)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;

                foreach ((_, (_, IDisposable? scope)) in cache)
                {
                    scope?.Dispose();
                }

                cache.Clear();

                foreach (ILoggerProvider loggerProvider in loggerProviders)
                {
                    loggerProvider.Dispose();
                }

                loggerProviders.Clear();
            }
        }

        private sealed class MulticastLogger : IMSLogger
        {
            private readonly IMSLogger[] decoratees;

            public MulticastLogger(IMSLogger[] decoratees)
            {
                this.decoratees = decoratees;
            }

            public bool IsEnabled(LogLevel logLevel) => decoratees.Any(x => x.IsEnabled(logLevel));

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                foreach (IMSLogger decoratee in decoratees)
                {
                    decoratee.Log(logLevel, eventId, state, exception, formatter);
                }
            }

            public IDisposable BeginScope<TState>(TState state)
                where TState : notnull
            {
                return new CompositeDisposable(decoratees.Select(x => x.BeginScope(state)).ToArray());
            }
        }
    }
}
