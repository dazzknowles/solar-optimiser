using System.Text.RegularExpressions;
using SolarOptimiser.Domain;

namespace SolarOptimiser.Collection
{
    /// <summary>
    /// The FoxESS-variable-to-<see cref="TelemetryQuantity"/> mapping set (SOL-T-702/705). The version increments
    /// for any change to this set, including a purely additive new mapping — see <c>MAPPING_CHANGELOG.md</c> in
    /// this project for what each version means.
    /// </summary>
    public static class FoxESSVariableMapping
    {
        public const short MappingVersion = 1;

        private static readonly Regex PVStringPattern = new Regex(@"^pv(\d+)Power$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly Dictionary<string, TelemetryQuantity> DirectMappings = new Dictionary<string, TelemetryQuantity>(StringComparer.Ordinal)
        {
            ["SoC"] = TelemetryQuantity.BatterySOC,
            ["invBatPower"] = TelemetryQuantity.BatteryPowerSigned,
            ["batDischargePower"] = TelemetryQuantity.BatteryDischargePower,
            ["batChargePower"] = TelemetryQuantity.BatteryChargePower,
            ["pvPower"] = TelemetryQuantity.PVPowerTotal,
            ["PVEnergyTotal"] = TelemetryQuantity.PVEnergyTotalCumulative,
            ["feedinPower"] = TelemetryQuantity.GridExportPower,
            ["gridConsumptionPower"] = TelemetryQuantity.GridImportPower,
            ["loadsPower"] = TelemetryQuantity.LoadPower,
            ["loads"] = TelemetryQuantity.LoadEnergyCumulative,
            ["meterPower"] = TelemetryQuantity.MeterPower,
            ["meterPower2"] = TelemetryQuantity.MeterPower2,

            // generationPower's physical meaning is explicitly uncertain per r04 (its own description carries a
            // question mark) until tenant-zero evidence resolves it - see MAPPING_CHANGELOG.md.
            ["generationPower"] = TelemetryQuantity.GenerationPowerAC,
            ["generation"] = TelemetryQuantity.GenerationEnergyCumulative
        };

        /// <summary>
        /// Maps a raw FoxESS variable name to a canonical quantity + raw vendor channel (SOL-T-703 - the channel
        /// is the numbered PV input's index only, never a physical-string assertion). Returns false for a variable
        /// this mapping set does not recognise - it is still recorded in <c>DeviceCapabilities</c> unmapped
        /// (SOL-T-302), it just produces no <c>TelemetryObservations</c> row.
        /// </summary>
        public static bool TryMap(string sourceVariable, out TelemetryQuantity quantity, out string? channel)
        {
            if (DirectMappings.TryGetValue(sourceVariable, out TelemetryQuantity direct))
            {
                quantity = direct;
                channel = null;
                return true;
            }

            Match match = PVStringPattern.Match(sourceVariable);
            if (match.Success)
            {
                quantity = TelemetryQuantity.PVStringPower;
                channel = match.Groups[1].Value;
                return true;
            }

            quantity = default;
            channel = null;
            return false;
        }
    }
}
