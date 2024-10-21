using Diginsight.Analyzer.Business.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Net;

namespace Diginsight.Analyzer.Business;

internal sealed partial class OrchestratorExecutionService : IOrchestratorExecutionService
{
    private static readonly MigrationException NoAgentAvailableException =
        new ("No agent available", HttpStatusCode.Conflict, IOrchestratorExecutionService.NoAgentAvailableExceptionLabel);

    private readonly ILogger logger;
    private readonly IOrchestratorLeaseService leaseService;
    private readonly IAgentClientFactory agentClientFactory;

    public OrchestratorExecutionService(
        ILogger<OrchestratorExecutionService> logger,
        IOrchestratorLeaseService leaseService,
        IAgentClientFactory agentClientFactory
    )
    {
        this.logger = logger;
        this.leaseService = leaseService;
        this.agentClientFactory = agentClientFactory;
    }

    public async Task<Guid> StartAsync(
        string? family,
        ExecutionKind kind,
        Func<ActiveLease, CancellationToken, Task<bool>> hasConflictAsync,
        Func<IAgentClient, Task<StartResponseBody>> coreStartAsync,
        CancellationToken cancellationToken
    )
    {
        using IDisposable? d0 = new LogScopeBuilder().With("ExecutionKind", kind).Begin(logger);

        LogMessages.GettingAvailableAgents(logger, family);

        IAsyncEnumerable<Agent> agents = leaseService.GetAgentsAE(family, hasConflictAsync, cancellationToken);
        await foreach (Agent agent in agents)
        {
            if (agent is ActiveAgent activeAgent)
            {
                if (activeAgent.IsConflicting)
                {
                    ExecutionKind otherKind = activeAgent.Kind;
                    Guid otherInstanceId = activeAgent.InstanceId;

                    LogMessages.ConflictingExecution(logger, otherKind, otherInstanceId);

                    throw MigrationExceptions.ConflictingExecution(otherKind, otherInstanceId);
                }

                continue;
            }

            IAgentClient agentClient = agentClientFactory.Make(agent.BaseAddress);

            StartResponseBody responseBody;
            try
            {
                responseBody = await coreStartAsync(agentClient);
            }
            catch (TimeoutException exception)
            {
                LogMessages.AgentTimeout(logger, agent.MachineName, exception);
                continue;
            }
            catch (MigrationException exception)
            {
                if (exception.InnerException is not MigrationException migrationException)
                {
                    throw;
                }

                string exceptionDescription = migrationException.Label;
                if (exceptionDescription == nameof(MigrationExceptions.AlreadyExecuting))
                {
                    continue;
                }

                if (exceptionDescription != nameof(MigrationExceptions.ConflictingExecution))
                {
                    throw;
                }

                object?[] exceptionParameters = migrationException.Parameters;
                ExecutionKind otherKind = JToken.FromObject(exceptionParameters[0]!).ToObject<ExecutionKind>();
                Guid otherInstanceId = JToken.FromObject(exceptionParameters[1]!).ToObject<Guid>();

                LogMessages.ConflictingExecution(logger, otherKind, otherInstanceId);

                throw MigrationExceptions.ConflictingExecution(otherKind, otherInstanceId);
            }

            return responseBody.InstanceId;
        }

        LogMessages.NoAgentAvailable(logger);
        throw NoAgentAvailableException;
    }

    public async Task<bool> DequeueAsync(Guid instanceId, string family, CancellationToken cancellationToken)
    {
        LogMessages.GettingAvailableAgents(logger, family);

        IAsyncEnumerable<Agent> agents = leaseService
            .GetAgentsAE(family, static (_, _) => Task.FromResult(false), cancellationToken)
            .Where(static x => x is not ActiveAgent);
        await foreach (Agent agent in agents.WithCancellation(cancellationToken))
        {
            IAgentClient agentClient = agentClientFactory.Make(agent.BaseAddress);

            try
            {
                await agentClient.DequeueMigrationAsync(instanceId);
            }
            catch (TimeoutException exception)
            {
                LogMessages.AgentTimeout(logger, agent.MachineName, exception);
                continue;
            }
            catch (MigrationException exception)
            {
                if (exception.InnerException is not MigrationException migrationException)
                {
                    throw;
                }

                string exceptionLabel = migrationException.Label;
                if (exceptionLabel == nameof(MigrationExceptions.AlreadyExecuting))
                {
                    continue;
                }

                if (exceptionLabel != nameof(MigrationExceptions.ConflictingExecution))
                {
                    throw;
                }

                object?[] exceptionParameters = migrationException.Parameters;
                ExecutionKind otherKind = JToken.FromObject(exceptionParameters[0]!).ToObject<ExecutionKind>();
                Guid otherInstanceId = JToken.FromObject(exceptionParameters[1]!).ToObject<Guid>();

                LogMessages.ConflictingExecution(logger, otherKind, otherInstanceId);

                throw MigrationExceptions.ConflictingExecution(otherKind, otherInstanceId);
            }

            return true;
        }

        return false;
    }

    public async Task<IEnumerable<Guid>> AbortAsync(ExecutionKind kind, Guid? instanceId)
    {
        ICollection<Guid> instanceIds = new List<Guid>();

        await foreach (Agent agent in leaseService.GetAllAgentsAE(CancellationToken.None))
        {
            IAgentClient agentClient = agentClientFactory.Make(agent.BaseAddress);
            AbortResponseBody responseBody;
            try
            {
                responseBody = kind switch
                {
                    ExecutionKind.Migration => await agentClient.AbortMigrationAsync(instanceId),
                    ExecutionKind.Deletion => await agentClient.AbortDeletionAsync(instanceId),
                    _ => throw new UnreachableException($"unrecognized {nameof(ExecutionKind)}"),
                };
            }
            catch (TimeoutException exception)
            {
                LogMessages.AgentTimeout(logger, agent.MachineName, exception);
                continue;
            }
            catch (MigrationException exception) when (instanceId is not null && IsNotFound(exception))
            {
                continue;
            }

            static bool IsNotFound(MigrationException exception)
            {
                return exception is
                {
                    Label: nameof(MigrationExceptions.DownstreamException),
#pragma warning disable SA1010
                    Parameters: [ HttpStatusCode.NotFound, .. ],
#pragma warning restore SA1010
                };
            }

            if (instanceId is not null)
            {
                return new[] { instanceId.Value };
            }

            instanceIds.AddRange(responseBody.InstanceIds);
        }

        return instanceIds;
    }

    private static partial class LogMessages
    {
        [LoggerMessage(0, LogLevel.Debug, "Looking for available agents in family {Family}")]
        internal static partial void GettingAvailableAgents(ILogger logger, string? family);

        [LoggerMessage(1, LogLevel.Information, "Found conflicting {Kind} execution with id {InstanceId}")]
        internal static partial void ConflictingExecution(ILogger logger, ExecutionKind kind, Guid instanceId);

        [LoggerMessage(2, LogLevel.Warning, "No agent available")]
        internal static partial void NoAgentAvailable(ILogger logger);

        [LoggerMessage(3, LogLevel.Warning, "Timeout from agent {MachineName}")]
        internal static partial void AgentTimeout(ILogger logger, string machineName, Exception exception);

        [LoggerMessage(7, LogLevel.Warning, "Duplicate instance id {InstanceId}")]
        internal static partial void DuplicateInstanceId(ILogger logger, Guid instanceId);
    }
}
