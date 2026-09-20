using System.Data;
using System.Data.Common;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Attributes;
using Eceni.Core.Base.Database.Common;
using Eceni.Core.Base.Database.Concrete;
using MySqlConnector;
using SolarOptimiser.Persistence.Abstract;
using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Persistence.Repositories
{
    internal sealed class DeviceCapabilityRow
    {
        [ColumnName]
        public long DeviceID { get; set; }

        [ColumnName]
        public string SourceVariable { get; set; } = string.Empty;

        [ColumnName]
        public string? Unit { get; set; }

        [ColumnName]
        public bool IsExpected { get; set; }

        [ColumnName]
        public DateTime? ExpectedSince { get; set; }

        [ColumnName]
        public DateTime? RetiredAtUTC { get; set; }

        [ColumnName]
        public DateTime DiscoveredAtUTC { get; set; }

        [ColumnName]
        public DateTime LastSeenAtUTC { get; set; }
    }

    public sealed class DeviceCapabilityRepository : IDeviceCapabilityRepository
    {
        private const string Prefix = "dc";

        private readonly IDBConnectionResolver _connectionResolver;

        public DeviceCapabilityRepository(IDBConnectionResolver connectionResolver)
        {
            _connectionResolver = connectionResolver;
        }

        public async Task UpsertSeenAsync(long deviceId, string sourceVariable, string? unit, DateTime seenAtUTC, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_deviceId", deviceId),
                new MySqlParameter("in_sourceVariable", sourceVariable),
                new MySqlParameter("in_unit", (object?)unit ?? DBNull.Value),
                new MySqlParameter("in_seenAtUTC", seenAtUTC)
            };

            await DBUtility.ExecuteNonQueryAsync(connection, CommandType.StoredProcedure, "espDeviceCapabilityUpsert", parameters);
        }

        public async Task ApproveAsync(long deviceId, string sourceVariable, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_deviceId", deviceId),
                new MySqlParameter("in_sourceVariable", sourceVariable)
            };

            await DBUtility.ExecuteNonQueryAsync(connection, CommandType.StoredProcedure, "espDeviceCapabilityApprove", parameters);
        }

        public async Task RetireAsync(long deviceId, string sourceVariable, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_deviceId", deviceId),
                new MySqlParameter("in_sourceVariable", sourceVariable)
            };

            await DBUtility.ExecuteNonQueryAsync(connection, CommandType.StoredProcedure, "espDeviceCapabilityRetire", parameters);
        }

        public async Task<IReadOnlyList<DeviceCapability>> GetByDeviceAsync(long deviceId, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_deviceId", deviceId)
            };

            List<DeviceCapability> capabilities = new List<DeviceCapability>();
            await foreach (IDataRecord record in DBUtility.ExecuteReaderAsync(connection, CommandType.StoredProcedure, "espDeviceCapabilityGetByDevice", parameters).WithCancellation(cancellationToken))
            {
                DeviceCapabilityRow row = DBUtilityCommon.ModelFromIDataRecord<DeviceCapabilityRow>(record, Prefix);
                capabilities.Add(new DeviceCapability(
                    row.DeviceID,
                    row.SourceVariable,
                    row.Unit,
                    row.IsExpected,
                    row.ExpectedSince,
                    row.RetiredAtUTC,
                    row.DiscoveredAtUTC,
                    row.LastSeenAtUTC));
            }

            return capabilities;
        }

        private DbConnection OpenConnection()
        {
            return DBUtility.CreateConnectionAsync(_connectionResolver.ResolveConnectionString(null), _connectionResolver.ResolveProvider(null));
        }
    }
}
