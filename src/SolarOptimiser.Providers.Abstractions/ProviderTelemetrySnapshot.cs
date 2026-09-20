namespace SolarOptimiser.Providers.Abstractions
{
    public sealed record ProviderTelemetrySnapshot(
        string ProviderDeviceID,
        DateTimeOffset RetrievedAtUTC,
        string? ProviderTimestampRaw,
        DateTimeOffset? ProviderTimestampParsed,
        IReadOnlyList<ProviderReading> Readings);
}
