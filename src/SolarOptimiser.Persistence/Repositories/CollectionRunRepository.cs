using System.Data;
using System.Data.Common;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Concrete;
using MySqlConnector;
using SolarOptimiser.Persistence.Abstract;
using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Persistence.Repositories
{
    public sealed class CollectionRunRepository : ICollectionRunRepository
    {
        private readonly IDBConnectionResolver _connectionResolver;

        public CollectionRunRepository(IDBConnectionResolver connectionResolver)
        {
            _connectionResolver = connectionResolver;
        }

        public async Task<long> StartAsync(DateTime startedAtUTC, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_startedAtUTC", startedAtUTC)
            };

            object? result = await DBUtility.ExecuteScalarAsync(connection, CommandType.StoredProcedure, "espCollectionRunStart", parameters);
            return Convert.ToInt64(result);
        }

        public async Task CompleteAsync(
            long collectionRunId,
            DateTime completedAtUTC,
            int devicesAttempted,
            int devicesSucceeded,
            int observationsWritten,
            string status,
            CollectionRunStatusEvidence? statusEvidence,
            CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_collectionRunId", collectionRunId),
                new MySqlParameter("in_completedAtUTC", completedAtUTC),
                new MySqlParameter("in_devicesAttempted", devicesAttempted),
                new MySqlParameter("in_devicesSucceeded", devicesSucceeded),
                new MySqlParameter("in_observationsWritten", observationsWritten),
                new MySqlParameter("in_status", status),
                new MySqlParameter("in_statusCheckedAtUTC", (object?)statusEvidence?.StatusCheckedAtUTC ?? DBNull.Value),
                new MySqlParameter("in_statusHTTPStatus", (object?)statusEvidence?.StatusHTTPStatus ?? DBNull.Value),
                new MySqlParameter("in_statusProviderErrorNumber", (object?)statusEvidence?.StatusProviderErrorNumber ?? DBNull.Value),
                new MySqlParameter("in_statusProviderMessage", (object?)statusEvidence?.StatusProviderMessage ?? DBNull.Value),
                new MySqlParameter("in_statusRequestPath", (object?)statusEvidence?.StatusRequestPath ?? DBNull.Value),
                new MySqlParameter("in_statusResponsePath", (object?)statusEvidence?.StatusResponsePath ?? DBNull.Value)
            };

            await DBUtility.ExecuteNonQueryAsync(connection, CommandType.StoredProcedure, "espCollectionRunComplete", parameters);
        }

        private DbConnection OpenConnection()
        {
            return DBUtility.CreateConnectionAsync(_connectionResolver.ResolveConnectionString(null), _connectionResolver.ResolveProvider(null));
        }
    }
}
