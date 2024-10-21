using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Net;

namespace Diginsight.Analyzer.Business;

internal sealed class AgentDeletionService : IAgentDeletionService
{
    private readonly IAgentExecutionService executionService;
    private readonly IOptionsMonitor<CoreConfig> coreConfigMonitor;

    public AgentDeletionService(
        IAgentExecutionService executionService,
        IOptionsMonitor<CoreConfig> coreConfigMonitor
    )
    {
        this.executionService = executionService;
        this.coreConfigMonitor = coreConfigMonitor;
    }

    private IAgentCoreConfig CoreConfig => coreConfigMonitor.CurrentValue;

    public Task<Guid> StartAsync(IEnumerable<Guid> siteIds, DeletionMode mode, IEnumerable<string> eventRecipients, CancellationToken cancellationToken)
    {
#if DEBUG
        if (CoreConfig.SkipAbilityInvocation && mode == DeletionMode.AbilityObjectsOnly)
        {
            throw new MigrationException("Won't delete Ability object since Ability invocation is turned off", HttpStatusCode.BadRequest, "WontDeleteAbilityObjects");
        }
#endif

        return executionService.StartAsync<DeletionLease>(
            ExecutionKind.Deletion,
            null,
            siteIds,
            static lease => { lease.Kind = ExecutionKind.Deletion; },
            static (_, _) => Task.FromResult(false),
            (instanceId, ct) => CoreStartAsync(instanceId, siteIds, mode, eventRecipients, ct),
            cancellationToken
        );
    }

    public Task<(ExecutionKind Kind, Guid InstanceId)?> GetCurrentAsync() => executionService.GetCurrentAsync();

    public Task<IEnumerable<Guid>> AbortAsync(Guid? instanceId)
    {
        return executionService.AbortAsync(ExecutionKind.Deletion, instanceId);
    }

    private Task CoreStartAsync(
        Guid instanceId,
        IEnumerable<Guid> siteIds,
        DeletionMode mode,
        IEnumerable<string> eventRecipients,
        CancellationToken cancellationToken
    )
    {
        return executionService.RunDetachedAsync(
            static sp => sp.GetRequiredService<IDeletionExecutor>(),
            (deletionExecutor, ct) => deletionExecutor
                .ExecuteAsync(instanceId, siteIds, mode, eventRecipients, ct),
            cancellationToken
        );
    }
}
