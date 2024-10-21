using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;

namespace Diginsight.Analyzer.Business;

internal sealed partial class InternalMigrationService : IInternalMigrationService
{
    private static readonly MigrationException CircularStepDependencyException =
        new ("Circular step dependency", HttpStatusCode.InternalServerError, "CircularStepDependency");

    private readonly ILogger logger;
    private readonly IEnumerable<IGlobalMigratorStep> globalMigratorSteps;
    private readonly IEnumerable<ISiteMigratorStep> siteMigratorSteps;
    private readonly IEnumerable<IMigratorStepGroup> migratorStepGroups;

    public InternalMigrationService(
        ILogger<InternalMigrationService> logger,
        IEnumerable<IGlobalMigratorStep> globalMigratorSteps,
        IEnumerable<ISiteMigratorStep> siteMigratorSteps,
        IEnumerable<IMigratorStepGroup> migratorStepGroups
    )
    {
        this.logger = logger;
        this.globalMigratorSteps = globalMigratorSteps;
        this.siteMigratorSteps = siteMigratorSteps;
        this.migratorStepGroups = migratorStepGroups;
    }

    public async Task<IEnumerable<IMigratorStep>> CalculateStepsAsync(
        StrongBox<GlobalInfo> globalInfoBox,
        IDictionary<Guid, SiteInfo> sites,
        IEnumerable<string>? globalStepNames,
        IEnumerable<string>? siteStepNames,
        CancellationToken cancellationToken
    )
    {
        LogMessages.SortingSteps(logger, globalStepNames, siteStepNames);

        IEnumerable<IStepDependencyObject> stepDependencyObjects = DummyStepDependencyObject.INSTANCES
            .Concat(globalMigratorSteps.Select(static x => new StepDependencyObject(x)))
            .Concat(siteMigratorSteps.Select(static x => new StepDependencyObject(x)))
            .Concat(migratorStepGroups.Select(static x => new StepGroupDependencyObject(true, x)))
            .Concat(migratorStepGroups.Select(static x => new StepGroupDependencyObject(false, x)));
        IEnumerable<(bool, string)> desiredStepKeys =
            (globalStepNames?.Select(static x => (true, x)) ?? globalMigratorSteps.Select(static x => (true, x.Name)))
            .Concat(siteStepNames?.Select(static x => (false, x)) ?? siteMigratorSteps.Select(static x => (false, x.Name)));

        IEnumerable<IMigratorStep> sortedMigratorSteps;
        try
        {
            sortedMigratorSteps = CommonUtils.SortByDependency(
                    stepDependencyObjects,
                    desiredStepKeys.Select(static x => (x.Item1, (string?)x.Item2)).ToArray()
                )
                .OfType<StepDependencyObject>()
                .Select(static x => x.Self)
                .ToArray();
        }
        catch (DependencyException<(bool IsGlobal, string? Name)> exception)
        {
            DependencyExceptionKind kind = exception.Kind;
            IEnumerable<string> names = exception.Keys.Select(static x => x.Name!).ToArray();

            throw kind switch
            {
                DependencyExceptionKind.UnknownObject =>
                    new MigrationException($"Unknown step '{exception.Keys.First().Name!}'", HttpStatusCode.BadRequest, "UnknownStep"),
                DependencyExceptionKind.UnknownObjectDependencies =>
                    new MigrationException($"Unknown step dependencies {new FormattableStringCollection(names)}", HttpStatusCode.InternalServerError, "UnknownStepDependencies"),
                DependencyExceptionKind.CircularDependency => CircularStepDependencyException,
                _ => new UnreachableException($"unrecognized {nameof(DependencyExceptionKind)}"),
            };
        }

        foreach (IMigratorStep migratorStep in sortedMigratorSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            LogMessages.ValidatingInput(logger, migratorStep.Name);
            await migratorStep.ValidateAsync(globalInfoBox, sites, cancellationToken);
        }

        return sortedMigratorSteps;
    }

    public void FillLease(MigrationLease lease, IEnumerable<IMigratorStep> sortedMigratorSteps)
    {
        lease.Kind = ExecutionKind.Migration;
        lease.GlobalSteps = sortedMigratorSteps.OfType<IGlobalMigratorStep>().Select(static x => x.Name).ToArray();
        lease.SiteSteps = sortedMigratorSteps.OfType<ISiteMigratorStep>().Select(static x => x.Name).ToArray();
    }

    public async Task<bool> HasConflictAsync(
        GlobalInfo globalInfo,
        IReadOnlyDictionary<Guid, SiteInfo> sites,
        ActiveLease lease,
        IEnumerable<IMigratorStep> migratorSteps,
        CancellationToken cancellationToken
    )
    {
        if (lease is not MigrationLease migrationLease)
        {
            return false;
        }

        foreach (IMigratorStep migratorStep in migratorSteps)
        {
            LogMessages.CheckingForConflicts(logger, migratorStep.Name);
            if (await migratorStep.HasConflictAsync(globalInfo, sites, migrationLease, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    public IEnumerable<IMigratorStep> NamesToSteps(IEnumerable<string> globalStepNames, IEnumerable<string> siteStepNames)
    {
        return globalStepNames.Select(n => globalMigratorSteps.First(s => s.Name == n))
            .Concat<IMigratorStep>(siteStepNames.Select(n => siteMigratorSteps.First(s => s.Name == n)))
            .ToArray();
    }

#pragma warning disable SA1201
    private interface IStepDependencyObject : IDependencyObject<(bool IsGlobal, string? Name)> { }
#pragma warning restore SA1201

    private sealed class DummyStepDependencyObject : IStepDependencyObject
    {
        public static readonly IEnumerable<IStepDependencyObject> INSTANCES = new IStepDependencyObject[]
        {
            new DummyStepDependencyObject(true), new DummyStepDependencyObject(false),
        };

        private DummyStepDependencyObject(bool isGlobal)
        {
            if (isGlobal)
            {
                Key = (true, null);
                Dependencies = Enumerable.Empty<(bool, string?)>();
            }
            else
            {
                Key = (false, null);
                Dependencies = new (bool, string?)[] { (true, null) };
            }
        }

        public (bool IsGlobal, string? Name) Key { get; }

        public IEnumerable<(bool IsGlobal, string? Name)> Dependencies { get; }
    }

    private sealed class StepDependencyObject : IStepDependencyObject
    {
        public StepDependencyObject(IGlobalMigratorStep self)
        {
            Self = self;
            Key = (true, self.Name);
            Dependencies = self.Dependencies.Select(static x => (true, (string?)x))
                .Append((true, null));
        }

        public StepDependencyObject(ISiteMigratorStep self)
        {
            Self = self;
            Key = (false, self.Name);
            Dependencies = self.GlobalDependencies.Select(static x => (true, (string?)x))
                .Concat(self.SiteDependencies.Select(static x => (false, (string?)x)))
                .Append((false, null));
        }

        public IMigratorStep Self { get; }

        public (bool IsGlobal, string? Name) Key { get; }

        public IEnumerable<(bool IsGlobal, string? Name)> Dependencies { get; }
    }

    private sealed class StepGroupDependencyObject : IStepDependencyObject
    {
        public StepGroupDependencyObject(bool isGlobal, IMigratorStepGroup group)
        {
            Key = (isGlobal, group.Name);
            Dependencies = isGlobal
                ? group.GlobalSteps.Select(static x => (true, (string?)x))
                : group.SiteSteps.Select(static x => (false, (string?)x));
        }

        public (bool IsGlobal, string? Name) Key { get; }

        public IEnumerable<(bool IsGlobal, string? Name)> Dependencies { get; }
    }

    private sealed class FormattableStringCollection : IFormattable
    {
        private readonly IEnumerable<string> underlying;

        public FormattableStringCollection(IEnumerable<string> underlying)
        {
            this.underlying = underlying;
        }

        public string ToString(string? format, IFormatProvider? formatProvider)
        {
            StringBuilder sb = new ("[");
            using (IEnumerator<string> enumerator = underlying.GetEnumerator())
            {
                if (enumerator.MoveNext())
                {
                    sb.Append($"'{enumerator.Current}'");
                    while (enumerator.MoveNext())
                    {
                        sb.Append($", '{enumerator.Current}'");
                    }
                }
            }

            sb.Append(']');
            return sb.ToString();
        }
    }

#pragma warning disable SA1204
    private static partial class LogMessages
#pragma warning restore SA1204
    {
        [LoggerMessage(0, LogLevel.Debug, "Sorting steps by depencency; desired global steps are {GlobalSteps}; desired site steps are {SiteSteps}")]
        internal static partial void SortingSteps(ILogger logger, IEnumerable<string>? globalSteps, IEnumerable<string>? siteSteps);

        [LoggerMessage(1, LogLevel.Debug, "Validating input with step {Step}")]
        internal static partial void ValidatingInput(ILogger logger, string step);

        [LoggerMessage(2, LogLevel.Debug, "Checking for conflicts on step {Step}")]
        internal static partial void CheckingForConflicts(ILogger logger, string step);
    }
}
