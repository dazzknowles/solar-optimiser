namespace SolarOptimiser.Domain
{
    /// <summary>
    /// Canonical telemetry quantities Phase 1 maps FoxESS variables onto (SOL-T-705). The product quantities
    /// (everything above the diagnostic group) are consumed by product features; the diagnostic/corroboration
    /// group is collected via the same mechanism but not yet used by any product feature (SOL-F-503).
    /// </summary>
    public enum TelemetryQuantity
    {
        // Product quantities
        BatterySOC,
        BatteryPowerSigned,
        BatteryChargePower,
        BatteryDischargePower,
        PVPowerTotal,
        PVStringPower,
        PVEnergyTotalCumulative,
        GridImportPower,
        GridExportPower,
        LoadPower,
        LoadEnergyCumulative,

        // Diagnostic / corroboration quantities
        MeterPower,
        MeterPower2,
        GenerationPowerAC,
        GenerationEnergyCumulative
    }
}
