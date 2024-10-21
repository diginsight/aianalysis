namespace Diginsight.Analyzer.Business;

public interface IAmbientService
{
    DateTime UtcNow { get; }

    Guid NewUlid();
}
