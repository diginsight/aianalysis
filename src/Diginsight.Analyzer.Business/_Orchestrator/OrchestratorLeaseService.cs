using Diginsight.Analyzer.Business.Models;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal partial class OrchestratorLeaseService : IOrchestratorLeaseService
{
    private readonly ILogger logger;
    private readonly IRepository<Lease> leaseRepository;

    public OrchestratorLeaseService(
        ILogger<OrchestratorLeaseService> logger,
        IRepository<Lease> leaseRepository
    )
    {
        this.logger = logger;
        this.leaseRepository = leaseRepository;
    }

    public async IAsyncEnumerable<Agent> GetAgentsAE(
        string? family,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        LogMessages.GettingAgentAvailabilities(logger);

        Func<IQueryable<Lease>, IQueryable<Lease>> transform;
        if (family is not null)
        {
            transform = q => q.Where(x => x.Kind != null || x.Family == family);
        }
        else
        {
            transform = static q => q;
        }

        await foreach (Lease lease in leaseRepository.GetItemsAE<Lease>(transform, cancellationToken))
        {
            if (lease.AsActive() is not { } otherLease)
            {
                yield return new Agent()
                {
                    BaseAddress = lease.BaseAddress,
                    MachineName = lease.MachineName,
                    Family = lease.Family,
                };
            }
            else
            {
                yield return new ActiveAgent()
                {
                    BaseAddress = otherLease.BaseAddress,
                    MachineName = otherLease.MachineName,
                    Family = otherLease.Family,
                    Kind = otherLease.Kind!.Value,
                    InstanceId = otherLease.InstanceId,
                    IsConflicting = await hasConflictAsync(otherLease, cancellationToken),
                };
            }
        }
    }

    public IAsyncEnumerable<Agent> GetAllAgentsAE(CancellationToken cancellationToken)
    {
        LogMessages.GettingAgents(logger);

        return leaseRepository
            .GetItemsAE(static q => q.Select(static x => new { x.BaseAddress, x.MachineName, x.Family }), cancellationToken)
            .Select(static x => new Agent() { BaseAddress = x.BaseAddress, MachineName = x.MachineName, Family = x.Family });
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Getting agent availabilites")]
        internal static partial void GettingAgentAvailabilities(ILogger logger);

        [LoggerMessage(1, LogLevel.Debug, "Getting agents")]
        internal static partial void GettingAgents(ILogger logger);
    }
}
