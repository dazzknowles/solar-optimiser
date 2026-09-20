namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// The read shape for one <c>TelemetryObservations</c> row, as returned by the retrieval API (SOL-T-1001,
    /// SOL-T-1003, SOL-T-1004).
    /// </summary>
    public sealed record ObservationDetail(
        long ID,
        long CaptureID,
        string Quantity,
        string? Channel,
        decimal? ValueParsed,
        string? Unit,
        string Quality,
        string SourceVariable,
        short MappingVersion,
        string? ProviderTimestampRaw,
        DateTime? ObservedAtUTC,
        string ObservedAtParseStatus,
        DateTime RetrievedAtUTC);
}
