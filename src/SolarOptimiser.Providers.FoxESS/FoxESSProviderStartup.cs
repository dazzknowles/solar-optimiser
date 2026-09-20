using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolarOptimiser.Providers.Abstractions;

namespace SolarOptimiser.Providers.FoxESS
{
    public static class FoxESSProviderStartup
    {
        public static IServiceCollection AddSolarOptimiserProvidersFoxESS(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<FoxESSProviderOptions>(configuration.GetSection("FoxESSProviderOptions"));
            services.AddSingleton<FoxESSRequestSigner>();
            services.AddHttpClient<FoxESSHttpClient>();
            services.AddSingleton<ITelemetryProvider, FoxESSTelemetryProvider>();

            return services;
        }
    }
}
