using Microsoft.Extensions.Logging;

namespace Diginsight.Analyzer.Business;

internal interface IFileLoggerFactoryFactory
{
    ILoggerFactory MakeGlobal(DateTime startedAt, Guid instanceId);

    ILoggerFactory MakeSite(DateTime startedAt, Guid instanceId, Guid siteId);
}
