using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Persistence.Abstract
{
    public interface ICollectionRunRepository
    {
        Task<long> StartAsync(DateTime startedAtUTC, CancellationToken cancellationToken);

        Task CompleteAsync(
            long collectionRunId,
            DateTime completedAtUTC,
            int devicesAttempted,
            int devicesSucceeded,
            int observationsWritten,
            string status, // Success | PartialFailure | Failed
            CollectionRunStatusEvidence? statusEvidence,
            CancellationToken cancellationToken);
    }
}
