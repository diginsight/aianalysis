using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using System.Runtime.CompilerServices;

namespace Diginsight.Analyzer.Common;

public static class JsonSerializationGlobals
{
    [PublicAPI]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Adjust(JsonSerializerSettings settings)
    {
        Adjust(settings, settings.ContractResolver as DefaultContractResolver ?? new DefaultContractResolver());
    }

    [PublicAPI]
    public static void AdjustDefaultSettings()
    {
        JsonSerializerSettings settings = JsonConvert.DefaultSettings?.Invoke() ?? new JsonSerializerSettings();
        Adjust(settings);

        JsonConvert.DefaultSettings = () => settings;
    }

    private static void Adjust(JsonSerializerSettings settings, IContractResolver contractResolver)
    {
        settings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;

        settings.ContractResolver = new CustomContractResolver(contractResolver);

        settings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;

        settings.Converters.Add(new StringEnumConverter());
    }

    private sealed class CustomContractResolver : IContractResolver
    {
        private readonly IContractResolver decoratee;
        private readonly ISet<Type> seenTypes = new HashSet<Type>();

        public CustomContractResolver(IContractResolver decoratee)
        {
            this.decoratee = decoratee;
        }

        public JsonContract ResolveContract(Type type)
        {
            JsonContract contract = decoratee.ResolveContract(type);
            if (!seenTypes.Add(type))
            {
                return contract;
            }

            if (typeof(Exception).IsAssignableFrom(type))
            {
                JsonContainerContract exceptionContract = (JsonContainerContract)contract;
                exceptionContract.ItemTypeNameHandling = TypeNameHandling.Auto;
            }

            return contract;
        }
    }
}
