namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// The write shape for one <c>TelemetryObservations</c> row (SOL-T-401). <see cref="ICaptureRepository"/>
    /// assigns <c>CaptureID</c> once the owning <c>CollectionAttempts</c> row is written, so it is not part of
    /// this record.
    /// </summary>
    public sealed record TelemetryObservationRecord(
        string Quantity,
        string? Channel,
        decimal? ValueParsed,
        string? Unit,
        string Quality, // Ok | Missing | Invalid | Stale
        string SourceVariable,
        short MappingVersion,
        string? ProviderTimestampRaw,
        DateTime? ObservedAtUTC,
        string ObservedAtParseStatus, // Parsed | Unparseable | NotAttempted
        DateTime RetrievedAtUTC);
}
