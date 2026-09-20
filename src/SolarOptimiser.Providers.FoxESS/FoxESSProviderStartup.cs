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

            // Read directly from configuration rather than a strongly-typed CollectionOptions reference -
            // SolarOptimiser.Providers.FoxESS must not depend on SolarOptimiser.Collection (SOL-T-101's
            // dependencies flow the other way). Falls back to 30s if unset.
            int requestTimeoutSeconds = configuration.GetValue<int?>("CollectionOptions:RequestTimeoutSeconds") ?? 30;
            services.AddHttpClient<FoxESSHttpClient>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds);
            });

            services.AddSingleton<ITelemetryProvider, FoxESSTelemetryProvider>();

            return services;
        }
    }
}
