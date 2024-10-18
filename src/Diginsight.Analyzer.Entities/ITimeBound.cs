using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Diagnostics.CodeAnalysis;

namespace Diginsight.Analyzer.Entities;

public interface ITimeBound
{
    [DisallowNull]
    DateTime? FinishedAt { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    TimeBoundStatus Status { get; set; }
}
