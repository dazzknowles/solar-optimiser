using System.Data;
using System.Data.Common;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Attributes;
using Eceni.Core.Base.Database.Common;
using Eceni.Core.Base.Database.Concrete;
using MySqlConnector;
using SolarOptimiser.Domain;
using SolarOptimiser.Persistence.Abstract;

namespace SolarOptimiser.Persistence.Repositories
{
    internal sealed class DeviceRow
    {
        [ColumnName]
        public long ID { get; set; }

        [ColumnName]
        public long SiteID { get; set; }

        [ColumnName]
        public string ProviderDeviceID { get; set; } = string.Empty;

        [ColumnName]
        public string? ModuleSerial { get; set; }

        [ColumnName]
        public string Status { get; set; } = string.Empty;

        [ColumnName]
        public string? Model { get; set; }

        [ColumnName]
        public bool HasPV { get; set; }

        [ColumnName]
        public bool HasBattery { get; set; }

        [ColumnName]
        public string? LastAlertedOutcome { get; set; }

        [ColumnName]
        public DateTime CreatedAtUTC { get; set; }

        [ColumnName]
        public DateTime UpdatedAtUTC { get; set; }
    }

    public sealed class DeviceRepository : IDeviceRepository
    {
        private const string Prefix = "d";

        private readonly IDBConnectionResolver _connectionResolver;

        public DeviceRepository(IDBConnectionResolver connectionResolver)
        {
            _connectionResolver = connectionResolver;
        }

        public async Task<IReadOnlyList<Device>> GetBySiteAsync(long siteId, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_siteId", siteId)
            };

            List<Device> devices = new List<Device>();
            await foreach (IDataRecord record in DBUtility.ExecuteReaderAsync(connection, CommandType.StoredProcedure, "espDeviceGetBySite", parameters).WithCancellation(cancellationToken))
            {
                devices.Add(ToDomain(DBUtilityCommon.ModelFromIDataRecord<DeviceRow>(record, Prefix)));
            }

            return devices;
        }

        public async Task<Device?> GetByIdAsync(long deviceId, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_deviceId", deviceId)
            };

            DeviceRow? row = await ReadFirstOrDefaultAsync(connection, "espDeviceGetByID", parameters, cancellationToken);
            if (row == null)
            {
                return null;
            }

            return ToDomain(row);
        }

        public async Task<Device> UpsertAsync(
            long siteId,
            string providerDeviceId,
            string? moduleSerial,
            string status,
            string? model,
            bool hasPV,
            bool hasBattery,
            CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_siteId", siteId),
                new MySqlParameter("in_providerDeviceId", providerDeviceId),
                new MySqlParameter("in_moduleSerial", (object?)moduleSerial ?? DBNull.Value),
                new MySqlParameter("in_status", status),
                new MySqlParameter("in_model", (object?)model ?? DBNull.Value),
                new MySqlParameter("in_hasPV", hasPV),
                new MySqlParameter("in_hasBattery", hasBattery)
            };

            DeviceRow? row = await ReadFirstOrDefaultAsync(connection, "espDeviceUpsert", parameters, cancellationToken);
            if (row == null)
            {
                throw new InvalidOperationException("espDeviceUpsert did not return the upserted row.");
            }

            return ToDomain(row);
        }

        public async Task UpdateLastAlertedOutcomeAsync(long deviceId, string? outcome, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_deviceId", deviceId),
                new MySqlParameter("in_outcome", (object?)outcome ?? DBNull.Value)
            };

            await DBUtility.ExecuteNonQueryAsync(connection, CommandType.StoredProcedure, "espDeviceUpdateLastAlertedOutcome", parameters);
        }

        private DbConnection OpenConnection()
        {
            return DBUtility.CreateConnectionAsync(_connectionResolver.ResolveConnectionString(null), _connectionResolver.ResolveProvider(null));
        }

        private static async Task<DeviceRow?> ReadFirstOrDefaultAsync(DbConnection connection, string storedProcedure, MySqlParameter[] parameters, CancellationToken cancellationToken)
        {
            await foreach (IDataRecord record in DBUtility.ExecuteReaderAsync(connection, CommandType.StoredProcedure, storedProcedure, parameters).WithCancellation(cancellationToken))
            {
                return DBUtilityCommon.ModelFromIDataRecord<DeviceRow>(record, Prefix);
            }

            return null;
        }

        private static Device ToDomain(DeviceRow row)
        {
            return new Device
            {
                ID = row.ID,
                SiteID = row.SiteID,
                ProviderDeviceID = row.ProviderDeviceID,
                ModuleSerial = row.ModuleSerial,
                Status = row.Status,
                Model = row.Model,
                HasPV = row.HasPV,
                HasBattery = row.HasBattery,
                LastAlertedOutcome = row.LastAlertedOutcome,
                CreatedAtUTC = row.CreatedAtUTC,
                UpdatedAtUTC = row.UpdatedAtUTC
            };
        }
    }
}
