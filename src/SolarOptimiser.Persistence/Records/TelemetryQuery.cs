namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// Request shape for SOL-T-1002's ranged query. <see cref="From"/>/<see cref="To"/> filter on
    /// <c>RetrievedAtUTC</c> (not <c>ObservedAtUTC</c>, which can be null/unparseable). Ordered
    /// <c>(RetrievedAtUTC, ID)</c> ascending — a total, stable order, since <c>RetrievedAtUTC</c> alone is not
    /// unique (every row from one capture shares it).
    /// </summary>
    public sealed record TelemetryQueryRequest(
        long? DeviceID,
        string? Quantity,
        string? Channel,
        DateTime? From,
        DateTime? To,
        int Limit,
        string? Cursor);

    public sealed record TelemetryQueryPage(IReadOnlyList<ObservationDetail> Items, string? NextCursor);
}
