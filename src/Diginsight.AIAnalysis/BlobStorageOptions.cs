using JetBrains.Annotations;

namespace Diginsight.AIAnalysis;

public sealed class BlobStorageOptions : IBlobStorageOptions
{
    [PublicAPI]
    public string? ConnectionString { get; set; }

    string IBlobStorageOptions.ConnectionString => ConnectionString ?? throw new InvalidOperationException($"{nameof(ConnectionString)} is unset");

    [PublicAPI]
    public string? ContainerPath { get; set; }

    string IBlobStorageOptions.ContainerPath => ContainerPath ?? throw new InvalidOperationException($"{nameof(ContainerPath)} is unset");

    [PublicAPI]
    public string? UntitledBlobNameFormat { get; set; } = "{0:yy}/{0:MM}/{0:dd}/{1}";

    string IBlobStorageOptions.UntitledBlobNameFormat => UntitledBlobNameFormat ?? throw new InvalidOperationException($"{nameof(UntitledBlobNameFormat)} is unset");

    [PublicAPI]
    public string? TitledBlobNameFormat { get; set; } = "{0:yy}/{0:MM}/{0:dd}/{1}";

    string IBlobStorageOptions.TitledBlobNameFormat => TitledBlobNameFormat ?? throw new InvalidOperationException($"{nameof(TitledBlobNameFormat)} is unset");
}
