namespace SolarOptimiser.Providers.Abstractions
{
    /// <summary>
    /// A closed, provider-neutral contract with no generic/arbitrary-request capability (SOL-T-201).
    /// <see cref="DiscoverDevicesAsync"/> and <see cref="GetLatestTelemetryAsync"/> mirror the FoxESS
    /// <c>device/list</c> (discovery/status) and <c>device/real/query</c> (telemetry) calls: two distinct provider
    /// calls with independent evidence, neither allowed to overwrite or be conflated with the other
    /// (SOL-T-401/SOL-T-402).
    /// </summary>
    public interface ITelemetryProvider
    {
        string ProviderKey { get; } // "FoxESS"

        Task<ProviderDiscoveryResult> DiscoverDevicesAsync(string providerSiteId, CancellationToken ct);

        Task<ProviderCallResult> GetLatestTelemetryAsync(string providerDeviceId, CancellationToken ct);
    }
}
