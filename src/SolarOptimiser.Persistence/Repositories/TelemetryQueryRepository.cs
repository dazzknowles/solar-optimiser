using System.Data;
using System.Data.Common;
using System.Text;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Attributes;
using Eceni.Core.Base.Database.Common;
using Eceni.Core.Base.Database.Concrete;
using MySqlConnector;
using SolarOptimiser.Persistence.Abstract;
using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Persistence.Repositories
{
    internal sealed class LatestHeaderRow
    {
        [ColumnName]
        public long DeviceID { get; set; }

        [ColumnName]
        public string CaptureOutcome { get; set; } = string.Empty;
    }

    internal sealed class ObservationRow
    {
        [ColumnName]
        public long ID { get; set; }

        [ColumnName]
        public long CaptureID { get; set; }

        [ColumnName]
        public string Quantity { get; set; } = string.Empty;

        [ColumnName]
        public string? Channel { get; set; }

        [ColumnName]
        public decimal? ValueParsed { get; set; }

        [ColumnName]
        public string? Unit { get; set; }

        [ColumnName]
        public string Quality { get; set; } = string.Empty;

        [ColumnName]
        public string SourceVariable { get; set; } = string.Empty;

        [ColumnName]
        public short MappingVersion { get; set; }

        [ColumnName]
        public string? ProviderTimestampRaw { get; set; }

        [ColumnName]
        public DateTime? ObservedAtUTC { get; set; }

        [ColumnName]
        public string ObservedAtParseStatus { get; set; } = string.Empty;

        [ColumnName]
        public DateTime RetrievedAtUTC { get; set; }
    }

    internal sealed class AttemptRow
    {
        [ColumnName]
        public long ID { get; set; }

        [ColumnName]
        public long CollectionRunID { get; set; }

        [ColumnName]
        public long DeviceID { get; set; }

        [ColumnName]
        public DateTime RequestedAtUTC { get; set; }

        [ColumnName]
        public DateTime? CompletedAtUTC { get; set; }

        [ColumnName]
        public string Outcome { get; set; } = string.Empty;

        [ColumnName]
        public string? DeviceStatus { get; set; }

        [ColumnName]
        public string? RequestedVariables { get; set; }

        [ColumnName]
        public string? ReturnedVariables { get; set; }
    }

    internal sealed class NearestIdRow
    {
        [ColumnName]
        public long ID { get; set; }
    }

    public sealed class TelemetryQueryRepository : ITelemetryQueryRepository
    {
        private const int DefaultLimit = 500;
        private const int MaxLimit = 5000;

        private readonly IDBConnectionResolver _connectionResolver;

        public TelemetryQueryRepository(IDBConnectionResolver connectionResolver)
        {
            _connectionResolver = connectionResolver;
        }

        public async Task<IReadOnlyList<TelemetryLatestRecord>> GetLatestAsync(
            long siteId,
            long? deviceId,
            string? quantity,
            CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_siteId", siteId),
                new MySqlParameter("in_deviceId", (object?)deviceId ?? DBNull.Value),
                new MySqlParameter("in_quantity", (object?)quantity ?? DBNull.Value)
            };

            List<TelemetryLatestRecord> results = new List<TelemetryLatestRecord>();
            await foreach (IDataRecord record in DBUtility.ExecuteReaderAsync(connection, CommandType.StoredProcedure, "espTelemetryObservationGetLatest", parameters).WithCancellation(cancellationToken))
            {
                LatestHeaderRow header = DBUtilityCommon.ModelFromIDataRecord<LatestHeaderRow>(record, "l");
                ObservationRow observation = DBUtilityCommon.ModelFromIDataRecord<ObservationRow>(record, "o");
                results.Add(new TelemetryLatestRecord(header.DeviceID, header.CaptureOutcome, ToDetail(observation)));
            }

            return results;
        }

        public async Task<TelemetryQueryPage> QueryAsync(TelemetryQueryRequest request, CancellationToken cancellationToken)
        {
            int effectiveLimit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);
            (DateTime? cursorRetrievedAtUTC, long? cursorId) = DecodeCursor(request.Cursor);

            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_deviceId", (object?)request.DeviceID ?? DBNull.Value),
                new MySqlParameter("in_quantity", (object?)request.Quantity ?? DBNull.Value),
                new MySqlParameter("in_channel", (object?)request.Channel ?? DBNull.Value),
                new MySqlParameter("in_from", (object?)request.From ?? DBNull.Value),
                new MySqlParameter("in_to", (object?)request.To ?? DBNull.Value),
                new MySqlParameter("in_cursorRetrievedAtUTC", (object?)cursorRetrievedAtUTC ?? DBNull.Value),
                new MySqlParameter("in_cursorId", (object?)cursorId ?? DBNull.Value),
                new MySqlParameter("in_limit", effectiveLimit)
            };

            List<ObservationDetail> items = new List<ObservationDetail>();
            await foreach (IDataRecord record in DBUtility.ExecuteReaderAsync(connection, CommandType.StoredProcedure, "espTelemetryObservationQuery", parameters).WithCancellation(cancellationToken))
            {
                items.Add(ToDetail(DBUtilityCommon.ModelFromIDataRecord<ObservationRow>(record, "o")));
            }

            string? nextCursor = items.Count == effectiveLimit
                ? EncodeCursor(items[items.Count - 1].RetrievedAtUTC, items[items.Count - 1].ID)
                : null;

            return new TelemetryQueryPage(items, nextCursor);
        }

        public async Task<NearestCaptureResult?> GetNearestCaptureAsync(
            long deviceId,
            DateTime at,
            TimeSpan maxDistance,
            CancellationToken cancellationToken)
        {
            DbConnection connection = OpenConnection();
            MySqlParameter[] parameters = new MySqlParameter[]
            {
                new MySqlParameter("in_deviceId", deviceId),
                new MySqlParameter("in_at", at),
                new MySqlParameter("in_maxDistanceSeconds", (int)maxDistance.TotalSeconds)
            };

            long? nearestId = null;
            await foreach (IDataRecord record in DBUtility.ExecuteReaderAsync(connection, CommandType.StoredProcedure, "espCollectionAttemptGetNearest", parameters).WithCancellation(cancellationToken))
            {
                nearestId = DBUtilityCommon.ModelFromIDataRecord<NearestIdRow>(record, "n").ID;
            }

            if (nearestId == null)
            {
                return null;
            }

            CaptureDetail? capture = await GetCaptureAsync(nearestId.Value, cancellationToken);
            if (capture == null)
            {
                return null;
            }

            return new NearestCaptureResult(capture, capture.RequestedAtUTC - at);
        }

        public async Task<CaptureDetail?> GetCaptureAsync(long captureId, CancellationToken cancellationToken)
        {
            DbConnection attemptConnection = OpenConnection();
            MySqlParameter[] attemptParameters = new MySqlParameter[]
            {
                new MySqlParameter("in_captureId", captureId)
            };

            AttemptRow? attempt = null;
            await foreach (IDataRecord record in DBUtility.ExecuteReaderAsync(attemptConnection, CommandType.StoredProcedure, "espCollectionAttemptGetByID", attemptParameters).WithCancellation(cancellationToken))
            {
                attempt = DBUtilityCommon.ModelFromIDataRecord<AttemptRow>(record, "a");
            }

            if (attempt == null)
            {
                return null;
            }

            DbConnection observationsConnection = OpenConnection();
            MySqlParameter[] observationParameters = new MySqlParameter[]
            {
                new MySqlParameter("in_captureId", captureId)
            };

            List<ObservationDetail> observations = new List<ObservationDetail>();
            await foreach (IDataRecord record in DBUtility.ExecuteReaderAsync(observationsConnection, CommandType.StoredProcedure, "espTelemetryObservationGetByCapture", observationParameters).WithCancellation(cancellationToken))
            {
                observations.Add(ToDetail(DBUtilityCommon.ModelFromIDataRecord<ObservationRow>(record, "o")));
            }

            return new CaptureDetail(
                attempt.ID,
                attempt.CollectionRunID,
                attempt.DeviceID,
                attempt.RequestedAtUTC,
                attempt.CompletedAtUTC,
                attempt.Outcome,
                attempt.DeviceStatus,
                attempt.RequestedVariables,
                attempt.ReturnedVariables,
                observations);
        }

        private DbConnection OpenConnection()
        {
            return DBUtility.CreateConnectionAsync(_connectionResolver.ResolveConnectionString(null), _connectionResolver.ResolveProvider(null));
        }

        private static ObservationDetail ToDetail(ObservationRow row)
        {
            return new ObservationDetail(
                row.ID,
                row.CaptureID,
                row.Quantity,
                row.Channel,
                row.ValueParsed,
                row.Unit,
                row.Quality,
                row.SourceVariable,
                row.MappingVersion,
                row.ProviderTimestampRaw,
                row.ObservedAtUTC,
                row.ObservedAtParseStatus,
                row.RetrievedAtUTC);
        }

        private static string EncodeCursor(DateTime retrievedAtUTC, long id)
        {
            string raw = $"{retrievedAtUTC.Ticks}|{id}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        }

        private static (DateTime? RetrievedAtUTC, long? ID) DecodeCursor(string? cursor)
        {
            if (string.IsNullOrEmpty(cursor))
            {
                return (null, null);
            }

            string raw = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            string[] parts = raw.Split('|');
            if (parts.Length != 2)
            {
                throw new FormatException("Cursor is not in the expected format.");
            }

            long ticks = long.Parse(parts[0]);
            long id = long.Parse(parts[1]);
            return (new DateTime(ticks, DateTimeKind.Utc), id);
        }
    }
}
