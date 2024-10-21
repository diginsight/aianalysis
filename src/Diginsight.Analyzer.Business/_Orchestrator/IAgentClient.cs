using Diginsight.Analyzer.Business.Models;

namespace Diginsight.Analyzer.Business;

internal interface IAgentClient
{
    Task<StartResponseBody> StartMigrationAsync(StartMigrationRequestBody body, IEnumerable<string> eventRecipients);

    Task DequeueMigrationAsync(Guid instanceId);

    Task<StartResponseBody> StartDeletionAsync(StartDeletionRequestBody body, IEnumerable<string> eventRecipients);

    Task<AbortResponseBody> AbortMigrationAsync(Guid? instanceId);

    Task<AbortResponseBody> AbortDeletionAsync(Guid? instanceId);
}
