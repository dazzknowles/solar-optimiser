namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// One row of the SOL-T-1001 "latest per device/quantity/channel" result: an <see cref="ObservationDetail"/>
    /// joined to the owning capture's device and outcome, since "latest per device/quantity" is served by joining
    /// <c>CollectionAttempts(DeviceID, RequestedAtUTC DESC)</c> into the observation table's capture index
    /// (SOL-T-401/SOL-T-402 — <c>TelemetryObservations</c> never denormalizes <c>DeviceID</c>).
    /// </summary>
    public sealed record TelemetryLatestRecord(
        long DeviceID,
        string CaptureOutcome,
        ObservationDetail Observation);
}
