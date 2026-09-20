using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Persistence.Abstract
{
    /// <summary>
    /// Writes one device's capture within a run: the <c>CollectionAttempts</c> row and, when there are any, its
    /// <c>TelemetryObservations</c> rows, inside a single database transaction via the Eceni.Core
    /// <c>IDbUnitOfWork</c> (SOL-T-505) — never <c>System.Transactions</c>/<c>TransactionScope</c>. A whole-attempt
    /// failure with nothing to persist writes only the <c>CollectionAttempts</c> row (a single statement needs no
    /// transaction).
    /// </summary>
    public interface ICaptureRepository
    {
        Task<long> RecordCaptureAsync(
            CollectionAttemptRecord attempt,
            IReadOnlyList<TelemetryObservationRecord> observations,
            CancellationToken cancellationToken);
    }
}
