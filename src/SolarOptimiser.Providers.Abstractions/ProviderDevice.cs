namespace SolarOptimiser.Providers.Abstractions
{
    public sealed record ProviderDevice(
        string ProviderDeviceID,
        string ProviderSiteID,
        string Status,
        string? ModuleSerial,
        bool HasPV,
        bool HasBattery,
        string? Model);
}
