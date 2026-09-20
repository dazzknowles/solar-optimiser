using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Persistence.Abstract
{
    /// <summary>
    /// The read side of the retrieval API (SOL-T-1001-SOL-T-1004), backing
    /// <c>SolarOptimiser.Host</c>'s endpoints.
    /// </summary>
    public interface ITelemetryQueryRepository
    {
        /// <summary>SOL-T-1001: latest row per (device, quantity, channel), newest first.</summary>
        Task<IReadOnlyList<TelemetryLatestRecord>> GetLatestAsync(
            long siteId,
            long? deviceId,
            string? quantity,
            CancellationToken cancellationToken);

        /// <summary>SOL-T-1002: cursor-paginated ranged query, ascending by (RetrievedAtUTC, ID).</summary>
        Task<TelemetryQueryPage> QueryAsync(TelemetryQueryRequest request, CancellationToken cancellationToken);

        /// <summary>
        /// SOL-T-1003: the capture nearest <paramref name="at"/> for <paramref name="deviceId"/>, comparing
        /// against <c>RequestedAtUTC</c>; ties break to the smaller absolute difference then the higher capture
        /// ID. Returns null when no capture is within <paramref name="maxDistance"/>.
        /// </summary>
        Task<NearestCaptureResult?> GetNearestCaptureAsync(
            long deviceId,
            DateTime at,
            TimeSpan maxDistance,
            CancellationToken cancellationToken);

        /// <summary>SOL-T-1004: one capture's full detail by its known ID.</summary>
        Task<CaptureDetail?> GetCaptureAsync(long captureId, CancellationToken cancellationToken);
    }
}
