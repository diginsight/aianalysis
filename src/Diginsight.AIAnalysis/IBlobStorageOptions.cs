namespace Diginsight.AIAnalysis;

public interface IBlobStorageOptions
{
    string ConnectionString { get; }
    string ContainerPath { get; }
    string UntitledBlobNameFormat { get; }
    string TitledBlobNameFormat { get; }
}
