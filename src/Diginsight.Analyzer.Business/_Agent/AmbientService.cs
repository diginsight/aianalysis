namespace Diginsight.Analyzer.Business;

internal sealed class AmbientService : IAmbientService
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTimeOffset OffsetUtcNow => DateTimeOffset.UtcNow;

    public Guid NewGuid() => Guid.NewGuid();
}
