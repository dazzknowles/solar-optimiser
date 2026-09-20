using Eceni.Core.Base.Configuration;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Concrete;
using Eceni.Core.Database.MySQL;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SolarOptimiser.Persistence.Abstract;
using SolarOptimiser.Persistence.Repositories;

namespace SolarOptimiser.Persistence
{
    public static class SolarOptimiserPersistenceStartup
    {
        /// <summary>
        /// Registers the MySQL <c>IDBUtility</c> provider and every repository, reading the connection string
        /// from the standard ASP.NET Core <c>ConnectionStrings:SolarOptimiser</c> configuration key (SOL-T-1201).
        /// </summary>
        public static IServiceCollection AddSolarOptimiserPersistence(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddEceniCoreDatabaseMySQL();

            DatabaseConfig databaseConfig = new DatabaseConfig
            {
                ConnectionString = configuration.GetConnectionString("SolarOptimiser"),
                Provider = DBUtilityMySQL.ProviderName
            };
            services.AddSingleton(Options.Create(databaseConfig));
            services.AddSingleton<IDBConnectionResolver, DBConnectionResolver>();

            services.AddSingleton<ISiteRepository, SiteRepository>();
            services.AddSingleton<IDeviceRepository, DeviceRepository>();
            services.AddSingleton<IDeviceCapabilityRepository, DeviceCapabilityRepository>();
            services.AddSingleton<IDeviceBatteryRepository, DeviceBatteryRepository>();
            services.AddSingleton<ICollectionRunRepository, CollectionRunRepository>();
            services.AddSingleton<ICaptureRepository, CaptureRepository>();
            services.AddSingleton<ITelemetryQueryRepository, TelemetryQueryRepository>();

            return services;
        }
    }
}
