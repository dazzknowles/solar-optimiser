using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SolarOptimiser.Collection
{
    public static class SolarOptimiserCollectionStartup
    {
        public static IServiceCollection AddSolarOptimiserCollection(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<CollectionOptions>(configuration.GetSection("CollectionOptions"));
            services.AddSingleton<ICollectionRunner, CollectionRunner>();

            return services;
        }
    }
}
