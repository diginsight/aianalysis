namespace Diginsight.Analyzer.Business;

internal interface IAgentExecutionService : IExecutionService
{
    Task<Guid> StartAsync<TLease>(
        ExecutionKind kind,
        Guid? requestedInstanceId,
        IEnumerable<Guid> siteIds,
        Action<TLease> fillLease,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        Func<Guid, CancellationToken, Task> coreStartAsync,
        CancellationToken cancellationToken
    )
        where TLease : ActiveLease, new();

    Task RunDetachedAsync<TServices>(
        Func<IServiceProvider, TServices> getServices,
        Func<TServices, CancellationToken, Task> runAsync,
        CancellationToken cancellationToken
    );

    Task WaitForFinishAsync();

    Task<(ExecutionKind Kind, Guid InstanceId)?> GetCurrentAsync();
}
