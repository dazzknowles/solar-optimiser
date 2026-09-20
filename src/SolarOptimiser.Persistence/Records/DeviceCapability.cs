namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// One row of <c>DeviceCapabilities</c> (SOL-T-302/SOL-T-401): a variable seen on a device, mapped or not.
    /// </summary>
    public sealed record DeviceCapability(
        long DeviceID,
        string SourceVariable,
        string? Unit,
        bool IsExpected,
        DateTime? ExpectedSince,
        DateTime? RetiredAtUTC,
        DateTime DiscoveredAtUTC,
        DateTime LastSeenAtUTC);
}
