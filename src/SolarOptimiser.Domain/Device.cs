namespace SolarOptimiser.Domain
{
    /// <summary>
    /// A single monitored device (inverter) at a <see cref="Site"/>. Provider-free (SOL-T-301): identity
    /// (<see cref="ProviderDeviceID"/>/<see cref="ModuleSerial"/>) is preserved exactly as reported.
    /// </summary>
    public sealed class Device
    {
        public long ID { get; set; }

        public long SiteID { get; set; }

        public string ProviderDeviceID { get; set; } = string.Empty;

        public string? ModuleSerial { get; set; }

        public string Status { get; set; } = string.Empty;

        public string? Model { get; set; }

        public bool HasPV { get; set; }

        public bool HasBattery { get; set; }

        /// <summary>
        /// The last outcome an alert was raised for (SOL-T-902) — null if no alert has ever fired for this device.
        /// </summary>
        public string? LastAlertedOutcome { get; set; }

        public DateTime CreatedAtUTC { get; set; }

        public DateTime UpdatedAtUTC { get; set; }
    }
}
