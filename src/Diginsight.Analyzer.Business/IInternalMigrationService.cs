using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Business;

internal interface IInternalMigrationService
{
    Task<IEnumerable<IMigratorStep>> CalculateStepsAsync(
        StrongBox<GlobalInfo> globalInfoBox,
        IDictionary<Guid, SiteInfo> sites,
        IEnumerable<string>? globalStepNames,
        IEnumerable<string>? siteStepNames,
        CancellationToken cancellationToken
    );

    void FillLease(MigrationLease lease, IEnumerable<IMigratorStep> migratorSteps);

    Task<bool> HasConflictAsync(
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        ActiveLease lease,
        IEnumerable<IMigratorStep> migratorSteps,
        CancellationToken cancellationToken
    );

    IEnumerable<IMigratorStep> NamesToSteps(IEnumerable<string> globalStepNames, IEnumerable<string> siteStepNames);
}
