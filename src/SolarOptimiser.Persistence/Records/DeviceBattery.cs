namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// One row of <c>DeviceBatteries</c> (SOL-T-303/SOL-T-401): one physical battery serial reported in
    /// <c>device/detail</c>'s <c>batteryList</c>, independent of how battery telemetry is aggregated at the
    /// device level.
    /// </summary>
    public sealed record DeviceBattery(
        long DeviceID,
        string BatterySerial,
        string? BatteryType,
        string? Model,
        string? CapacityRaw,
        string? ManufacturedAtRaw,
        DateTime DiscoveredAtUTC);
}
