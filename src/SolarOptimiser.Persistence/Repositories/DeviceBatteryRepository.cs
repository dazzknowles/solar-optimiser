using System.Data;
using System.Data.Common;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Concrete;
using MySqlConnector;
using SolarOptimiser.Persistence.Abstract;

namespace SolarOptimiser.Persistence.Repositories
{
    public sealed class DeviceBatteryRepository : IDeviceBatteryRepository
    {
        private readonly IDBConnectionResolver _connectionResolver;

        public DeviceBatteryRepository(IDBConnectionResolver connectionResolver)
        {
            _connectionResolver = connectionResolver;
        }

        public async Task UpsertAsync(
            long deviceId,
            string batterySerial,
            string? batteryType,
            string? model,
            string? capacityRaw,
            string? manufacturedAtRaw,
            CancellationToken cancellationToken)
        {
            DbConnection connection = DBUtility.CreateConnectionAsync(_connectionResolver.ResolveConnectionString(null), _connectionResolver.ResolveProvider(null));
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_deviceId", deviceId),
                new MySqlParameter("in_batterySerial", batterySerial),
                new MySqlParameter("in_batteryType", (object?)batteryType ?? DBNull.Value),
                new MySqlParameter("in_model", (object?)model ?? DBNull.Value),
                new MySqlParameter("in_capacityRaw", (object?)capacityRaw ?? DBNull.Value),
                new MySqlParameter("in_manufacturedAtRaw", (object?)manufacturedAtRaw ?? DBNull.Value),
                new MySqlParameter("in_discoveredAtUTC", DateTime.UtcNow)
            };

            await DBUtility.ExecuteNonQueryAsync(connection, CommandType.StoredProcedure, "espDeviceBatteryUpsert", parameters);
        }
    }
}
