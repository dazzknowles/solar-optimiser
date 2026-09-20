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
    internal sealed class SiteRow
    {
        [ColumnName]
        public long ID { get; set; }

        [ColumnName]
        public string ProviderKey { get; set; } = string.Empty;

        [ColumnName]
        public string ProviderSiteID { get; set; } = string.Empty;

        [ColumnName]
        public string Name { get; set; } = string.Empty;

        [ColumnName]
        public string? TimeZone { get; set; }

        [ColumnName]
        public DateTime CreatedAtUTC { get; set; }

        [ColumnName]
        public DateTime UpdatedAtUTC { get; set; }
    }

    public sealed class SiteRepository : ISiteRepository
    {
        private const string Prefix = "s";

        private readonly IDBConnectionResolver _connectionResolver;

        public SiteRepository(IDBConnectionResolver connectionResolver)
        {
            _connectionResolver = connectionResolver;
        }

        public async Task<Site?> GetByProviderIdAsync(string providerKey, string providerSiteId, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_providerKey", providerKey),
                new MySqlParameter("in_providerSiteId", providerSiteId)
            };

            SiteRow? row = await ReadFirstOrDefaultAsync(connection, "espSiteGetByProviderID", parameters, cancellationToken);
            if (row == null)
            {
                return null;
            }

            return ToDomain(row);
        }

        public async Task<Site> UpsertAsync(string providerKey, string providerSiteId, string name, string? timeZone, CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_providerKey", providerKey),
                new MySqlParameter("in_providerSiteId", providerSiteId),
                new MySqlParameter("in_name", name),
                new MySqlParameter("in_timeZone", (object?)timeZone ?? DBNull.Value)
            };

            SiteRow? row = await ReadFirstOrDefaultAsync(connection, "espSiteUpsert", parameters, cancellationToken);
            if (row == null)
            {
                throw new InvalidOperationException("espSiteUpsert did not return the upserted row.");
            }

            return ToDomain(row);
        }

        private DbConnection OpenConnection()
        {
            return DBUtility.CreateConnectionAsync(_connectionResolver.ResolveConnectionString(null), _connectionResolver.ResolveProvider(null));
        }

        private static async Task<SiteRow?> ReadFirstOrDefaultAsync(DbConnection connection, string storedProcedure, MySqlParameter[] parameters, CancellationToken cancellationToken)
        {
            await foreach (IDataRecord record in DBUtility.ExecuteReaderAsync(connection, CommandType.StoredProcedure, storedProcedure, parameters).WithCancellation(cancellationToken))
            {
                return DBUtilityCommon.ModelFromIDataRecord<SiteRow>(record, Prefix);
            }

            return null;
        }

        private static Site ToDomain(SiteRow row)
        {
            return new Site
            {
                ID = row.ID,
                ProviderKey = row.ProviderKey,
                ProviderSiteID = row.ProviderSiteID,
                Name = row.Name,
                TimeZone = row.TimeZone,
                CreatedAtUTC = row.CreatedAtUTC,
                UpdatedAtUTC = row.UpdatedAtUTC
            };
        }
    }
}
