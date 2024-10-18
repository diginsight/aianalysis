using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Text;

namespace Diginsight.Analyzer.Common;

public static class Extensions
{
    private static readonly Encoding DefaultSerializationEncoding = new UTF8Encoding(false);

    [PublicAPI]
    public static bool TryToObject<T>(this JToken jtoken, out T? obj, JsonSerializer? serializer = null)
    {
        try
        {
            obj = jtoken.ToObject<T>(serializer ?? JsonSerializer.CreateDefault());
            return true;
        }
        catch (Exception)
        {
            obj = default;
            return false;
        }
    }

    [PublicAPI]
    public static bool TryToObject(this JToken jtoken, Type type, out object? obj, JsonSerializer? serializer = null)
    {
        try
        {
            obj = jtoken.ToObject(type, serializer ?? JsonSerializer.CreateDefault());
            return true;
        }
        catch (Exception)
        {
            obj = default;
            return false;
        }
    }

    [PublicAPI]
    public static void Serialize(
        this JsonSerializer serializer, Stream stream, object? obj, Type? objectType = null, Encoding? encoding = null
    )
    {
        using TextWriter tw = new StreamWriter(stream, encoding ?? DefaultSerializationEncoding, leaveOpen: true);
        using JsonWriter jw = new JsonTextWriter(tw);
        serializer.Serialize(jw, obj, objectType);
        tw.Flush();
    }

    [PublicAPI]
    public static async Task SerializeAsync(
        this JsonSerializer serializer, Stream stream, object? obj, Type? objectType = null, Encoding? encoding = null
    )
    {
        await using TextWriter tw = new StreamWriter(stream, encoding ?? DefaultSerializationEncoding, leaveOpen: true);
        await using JsonWriter jw = new JsonTextWriter(tw);
        serializer.Serialize(jw, obj, objectType);
        await tw.FlushAsync();
    }

    [PublicAPI]
    public static T Deserialize<T>(
        this JsonSerializer serializer, Stream stream, Encoding? encoding = null
    )
    {
        using TextReader tr = new StreamReader(stream, encoding ?? DefaultSerializationEncoding, leaveOpen: true);
        using JsonReader jr = new JsonTextReader(tr);
        return serializer.Deserialize<T>(jr)!;
    }

    [PublicAPI]
    public static async Task<T> DeserializeAsync<T>(
        this JsonSerializer serializer, Stream stream, Encoding? encoding = null
    )
    {
        using TextReader tr = new StreamReader(stream, encoding ?? DefaultSerializationEncoding, leaveOpen: true);
        await using JsonReader jr = new JsonTextReader(tr);
        return serializer.Deserialize<T>(jr)!;
    }
}
