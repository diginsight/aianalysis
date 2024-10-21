using Diginsight.Analyzer.Entities;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Diginsight.Analyzer.Business.Models;

public class StepReport
{
    public StepReport(string internalName, TimeBoundStatus status)
    {
        InternalName = internalName;
        Status = status;
    }

    public string InternalName { get; }

    [JsonConverter(typeof(StringEnumConverter))]
    public TimeBoundStatus Status { get; }
}

#pragma warning disable SA1402
public sealed class StepReport<TId> : StepReport
#pragma warning restore SA1402
    where TId : notnull
{
    public StepReport(string internalName, TimeBoundStatus status, IDictionary<TId, Problem> problems)
        : base(internalName, status)
    {
        Problems = problems;
    }

    [JsonConverter(typeof(ProblemsConverter))]
    public IDictionary<TId, Problem> Problems { get; }
}

#pragma warning disable SA1402
file class ProblemsConverter : JsonConverter
#pragma warning restore SA1402
{
    public override bool CanRead => false;

    public override bool CanWrite => true;

    public override object ReadJson(JsonReader reader, Type objectType, object? existingValue, JsonSerializer serializer)
    {
        throw new NotSupportedException();
    }

    public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
    {
        Type keyType = value!.GetType().GetInterfaces()
            .First(static x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IDictionary<,>))
            .GetGenericArguments()[0];

        object adjustedValue = keyType == typeof(JValue) || serializer.ContractResolver.ResolveContract(keyType) is JsonPrimitiveContract
            ? value
            : typeof(Enumerable)
                .GetMethod(nameof(Enumerable.ToArray))!
                .MakeGenericMethod(typeof(KeyValuePair<,>).MakeGenericType(keyType, typeof(Problem)))
                .Invoke(null, new object[] { value })!;
        serializer.Serialize(writer, adjustedValue);
    }

    public override bool CanConvert(Type objectType)
    {
        throw new NotSupportedException();
    }
}
