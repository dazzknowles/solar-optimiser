namespace SolarOptimiser.Domain
{
    /// <summary>
    /// Per-observation quality classification (SOL-T-602). <see cref="Stale"/> is a defined value that no code
    /// path currently sets (SOL-T-605) — no measured freshness threshold exists yet.
    /// </summary>
    public enum TelemetryQuality
    {
        Ok,
        Missing,
        Invalid,
        Stale
    }
}
