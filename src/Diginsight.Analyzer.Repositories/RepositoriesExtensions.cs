using Diginsight.Analyzer.Common;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Runtime.CompilerServices;
using System.Text;

namespace Diginsight.Analyzer.Repositories;

public static class RepositoriesExtensions
{
    public static IServiceCollection AddRepositories(this IServiceCollection services, IConfiguration configuration, bool isHuawei)
    {
        if (services.Any(static x => x.ServiceType == typeof(IAnalysisInfoRepository)))
        {
            return services;
        }

        services
            .AddSingleton<IAnalysisInfoRepository, AnalysisInfoRepository>();

        //services
        //    .AddSafeAsyncDistributedCache()
        //    .Configure<CosmosDbConfig>(configuration.GetSection("CosmosDb"))
        //    .AddSingleton<ICosmosDbContainerProvider>(
        //        static p => ActivatorUtilities.CreateInstance<EdcsAwareCosmosDbContainerProvider>(p, NewtonsoftJsonCosmosSerializer.Instance));

        //services
        //    .AddSingleton(typeof(IRepository<>), typeof(CosmosDbRepository<>))
        //    .AddScoped<IFileStorage, BlobFileStorage>();

        if (CommonUtils.IsAgent)
        {
            //services
            //    .Configure<ElcpModelConfig>(configuration.GetSection("ElcpModel"))
            //    .Configure<ABBAbilityConfiguration>(configuration.GetSection("ABBAbility"))
            //    .Configure<W1ABBAbilityConfiguration>(configuration.GetSection("ABBAbility_W1"))
            //    .Configure<BlobStorageConfig>(configuration.GetSection(ConfigurationPath.Combine("BlobStorage", "Wave2Storage02")))
            //    .AddSingleton<IElcpModelRepository, ElcpModelRepository>()
            //    .AddSingleton<IAbilityService, AbilityService>()
            //    .AddSingleton<IABBAbilityApiInformationModelClient, ABBAbilityApiInformationModelClient>()
            //    .AddSingleton(typeof(IGenericEdcsGlobalRepository<>), typeof(GenericEdcsGlobalRepository<>))
            //    .AddSingleton(typeof(IGenericEdcsPlantRepository<>), typeof(GenericEdcsPlantRepository<>))
            //    .AddSingleton(typeof(IGenericW1GlobalRepository<>), typeof(GenericW1GlobalRepository<>))
            //    .AddSingleton(typeof(IGenericW1PlantRepository<>), typeof(GenericW1PlantRepository<>))
            //    .AddSingleton(typeof(IGenericTargetGlobalRepository<,>), typeof(GenericTargetGlobalRepository<,>))
            //    .AddSingleton(typeof(IGenericTargetSiteRepository<,>), typeof(GenericTargetSiteRepository<,>));
        }

        return services;
    }

    private sealed class NewtonsoftJsonCosmosSerializer : CosmosSerializer
    {
        public static readonly CosmosSerializer Instance = new NewtonsoftJsonCosmosSerializer();

        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);
        private static readonly JsonSerializerSettings SerializerSettings;

        static NewtonsoftJsonCosmosSerializer()
        {
            JsonSerializerSettings serializerSettings = new (JsonConvert.DefaultSettings!());
            serializerSettings.ContractResolver = new CosmosContractResolver(serializerSettings.ContractResolver!);
            serializerSettings.Formatting = Formatting.None;
            SerializerSettings = serializerSettings;
        }

        private NewtonsoftJsonCosmosSerializer() { }

        public override T FromStream<T>(Stream stream)
        {
            using (stream)
            {
                if (typeof(Stream).IsAssignableFrom(typeof(T)))
                {
                    Stream stream0 = stream;
                    return Unsafe.As<Stream, T>(ref stream0);
                }

                return GetSerializer().Deserialize<T>(stream, encoding: DefaultEncoding);
            }
        }

        public override Stream ToStream<T>(T input)
        {
            MemoryStream memoryStream = new ();

            if (input is Stream inputAsStream)
            {
                inputAsStream.CopyTo(memoryStream);
            }
            else
            {
                GetSerializer().Serialize(memoryStream, input, encoding: DefaultEncoding);
            }

            memoryStream.Position = 0;

            return memoryStream;
        }

        private static JsonSerializer GetSerializer()
        {
            return JsonSerializer.Create(SerializerSettings);
        }

        private sealed class CosmosContractResolver : IContractResolver
        {
            private readonly IContractResolver decoratee;

            public CosmosContractResolver(IContractResolver decoratee)
            {
                this.decoratee = decoratee;
            }

            public JsonContract ResolveContract(Type type)
            {
                JsonContract contract = decoratee.ResolveContract(type);

                if (type == typeof(CosmosException))
                {
                    JsonObjectContract exceptionContract = (JsonObjectContract)contract;
                    exceptionContract.Properties.GetClosestMatchProperty(nameof(CosmosException.Diagnostics))!.Ignored = true;
                }

                return contract;
            }
        }
    }
}
