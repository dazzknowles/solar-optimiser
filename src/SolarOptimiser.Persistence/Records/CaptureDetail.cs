namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// The full detail of one capture (a <c>CollectionAttempts</c> row plus all of its
    /// <c>TelemetryObservations</c>), as returned by SOL-T-1003 (nearest-to-a-timestamp) and SOL-T-1004
    /// (by known ID).
    /// </summary>
    public sealed record CaptureDetail(
        long CaptureID,
        long CollectionRunID,
        long DeviceID,
        DateTime RequestedAtUTC,
        DateTime? CompletedAtUTC,
        string Outcome,
        string? DeviceStatus,
        string? RequestedVariables,
        string? ReturnedVariables,
        IReadOnlyList<ObservationDetail> Observations);
}
