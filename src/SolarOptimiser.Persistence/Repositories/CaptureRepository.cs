using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Concrete;
using MySqlConnector;
using System.Data;
using System.Data.Common;
using SolarOptimiser.Persistence.Abstract;
using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Persistence.Repositories
{
    public sealed class CaptureRepository : ICaptureRepository
    {
        private readonly IDBConnectionResolver _connectionResolver;

        public CaptureRepository(IDBConnectionResolver connectionResolver)
        {
            _connectionResolver = connectionResolver;
        }

        public async Task<long> RecordCaptureAsync(
            CollectionAttemptRecord attempt,
            IReadOnlyList<TelemetryObservationRecord> observations,
            CancellationToken cancellationToken)
        {
            if (observations.Count == 0)
            {
                return await InsertAttemptOnlyAsync(attempt, cancellationToken);
            }

            string connectionString = _connectionResolver.ResolveConnectionString(null);
            string providerKey = _connectionResolver.ResolveProvider(null) ?? string.Empty;

            await using (IDbUnitOfWork unitOfWork = await DBUtility.BeginUnitOfWorkAsync(connectionString, providerKey, cancellationToken))
            {
                long captureId = await unitOfWork.ExecuteScalarAsync<long>("espCollectionAttemptInsert", AttemptParameters(attempt), cancellationToken);

                foreach (TelemetryObservationRecord observation in observations)
                {
                    await unitOfWork.ExecuteAsync("espTelemetryObservationInsert", ObservationParameters(captureId, observation), cancellationToken);
                }

                await unitOfWork.CommitAsync(cancellationToken);
                return captureId;
            }
        }

        private async Task<long> InsertAttemptOnlyAsync(CollectionAttemptRecord attempt, CancellationToken cancellationToken)
        {
            await using (DbConnection connection = DBUtility.CreateConnectionAsync(_connectionResolver.ResolveConnectionString(null), _connectionResolver.ResolveProvider(null)))
            {
                object? result = await DBUtility.ExecuteScalarAsync(connection, CommandType.StoredProcedure, "espCollectionAttemptInsert", AttemptParameters(attempt));
                return Convert.ToInt64(result);
            }
        }

        private static MySqlParameter[] AttemptParameters(CollectionAttemptRecord attempt)
        {
            return new MySqlParameter[]
            {
                new MySqlParameter("in_collectionRunId", attempt.CollectionRunID),
                new MySqlParameter("in_deviceId", attempt.DeviceID),
                new MySqlParameter("in_requestedAtUTC", attempt.RequestedAtUTC),
                new MySqlParameter("in_completedAtUTC", (object?)attempt.CompletedAtUTC ?? DBNull.Value),
                new MySqlParameter("in_outcome", attempt.Outcome),
                new MySqlParameter("in_deviceStatus", (object?)attempt.DeviceStatus ?? DBNull.Value),
                new MySqlParameter("in_httpStatus", (object?)attempt.HTTPStatus ?? DBNull.Value),
                new MySqlParameter("in_providerErrorNumber", (object?)attempt.ProviderErrorNumber ?? DBNull.Value),
                new MySqlParameter("in_providerMessage", (object?)attempt.ProviderMessage ?? DBNull.Value),
                new MySqlParameter("in_requestedVariables", (object?)attempt.RequestedVariables ?? DBNull.Value),
                new MySqlParameter("in_returnedVariables", (object?)attempt.ReturnedVariables ?? DBNull.Value),
                new MySqlParameter("in_requestPath", (object?)attempt.RequestPath ?? DBNull.Value),
                new MySqlParameter("in_rawResponsePath", (object?)attempt.RawResponsePath ?? DBNull.Value),
                new MySqlParameter("in_sentryEventId", (object?)attempt.SentryEventID ?? DBNull.Value)
            };
        }

        private static MySqlParameter[] ObservationParameters(long captureId, TelemetryObservationRecord observation)
        {
            return new MySqlParameter[]
            {
                new MySqlParameter("in_captureId", captureId),
                new MySqlParameter("in_quantity", observation.Quantity),
                new MySqlParameter("in_channel", (object?)observation.Channel ?? DBNull.Value),
                new MySqlParameter("in_valueParsed", (object?)observation.ValueParsed ?? DBNull.Value),
                new MySqlParameter("in_unit", (object?)observation.Unit ?? DBNull.Value),
                new MySqlParameter("in_quality", observation.Quality),
                new MySqlParameter("in_sourceVariable", observation.SourceVariable),
                new MySqlParameter("in_mappingVersion", observation.MappingVersion),
                new MySqlParameter("in_providerTimestampRaw", (object?)observation.ProviderTimestampRaw ?? DBNull.Value),
                new MySqlParameter("in_observedAtUTC", (object?)observation.ObservedAtUTC ?? DBNull.Value),
                new MySqlParameter("in_observedAtParseStatus", observation.ObservedAtParseStatus),
                new MySqlParameter("in_retrievedAtUTC", observation.RetrievedAtUTC)
            };
        }
    }
}
