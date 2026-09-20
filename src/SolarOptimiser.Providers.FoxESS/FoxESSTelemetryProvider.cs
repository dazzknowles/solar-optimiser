using SolarOptimiser.Providers.Abstractions;
using SolarOptimiser.Providers.FoxESS.Contracts;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SolarOptimiser.Providers.FoxESS
{
    public sealed class FoxESSTelemetryProvider : ITelemetryProvider
    {
        // Matches the documented numbered PV-string channel variables only (pv1Power..pv24Power and their
        // matching volt/current fields, r04 §4.2/§5) — not applied generally, since deciding whether any other
        // trailing digit in a variable name is a "channel" versus part of the quantity's own identity (for
        // example MeterPower2, which SOL-T-705 treats as its own distinct quantity, not channel 2 of MeterPower)
        // is a FoxESS-variable-to-domain mapping decision that belongs to Collection, not this adapter.
        private static readonly Regex PVChannelPattern = new Regex(
            "^pv(?<channel>[0-9]+)(Power|Volt|Current)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly FoxESSHttpClient _httpClient;

        public FoxESSTelemetryProvider(FoxESSHttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public string ProviderKey => "FoxESS";

        public async Task<ProviderDiscoveryResult> DiscoverDevicesAsync(string providerSiteId, CancellationToken ct)
        {
            FoxESSApiCallResult<FoxESSDeviceListResponse> result = await _httpClient.GetDeviceListAsync(ct);

            ProviderOutcome outcome = ClassifyOutcome(result.HttpStatusCode, result.Parsed?.Errno, result.TransportFailed, result.ParseFailed);

            if (outcome != ProviderOutcome.Success || result.Parsed?.Result == null)
            {
                return new ProviderDiscoveryResult(
                    outcome,
                    (int?)result.HttpStatusCode,
                    result.Parsed?.Errno,
                    result.Parsed?.Msg,
                    result.RequestSanitized,
                    result.RawResponseSanitized,
                    null);
            }

            // device/list is account-wide across every station; only devices belonging to the requested site
            // are returned to the caller. Battery-list detail (SOL-T-303) is not obtainable through this
            // interface — see the note on ITelemetryProvider's scope in the technical spec; DeviceBatteries
            // stays unpopulated in Phase 1 unless the interface grows a call for it.
            List<ProviderDevice> devices = new List<ProviderDevice>();
            foreach (FoxESSDeviceListItem item in result.Parsed.Result.Data)
            {
                if (!string.Equals(item.StationID, providerSiteId, StringComparison.Ordinal))
                {
                    continue;
                }

                devices.Add(new ProviderDevice(
                    item.DeviceSN,
                    providerSiteId,
                    MapDeviceStatus(item.Status),
                    item.ModuleSN,
                    item.HasPV,
                    item.HasBattery,
                    item.DeviceType));
            }

            return new ProviderDiscoveryResult(
                ProviderOutcome.Success,
                (int?)result.HttpStatusCode,
                result.Parsed.Errno,
                result.Parsed.Msg,
                result.RequestSanitized,
                result.RawResponseSanitized,
                devices);
        }

        public async Task<ProviderCallResult> GetLatestTelemetryAsync(string providerDeviceId, CancellationToken ct)
        {
            FoxESSApiCallResult<FoxESSDeviceRealQueryResponse> result = await _httpClient.GetDeviceRealQueryAsync(providerDeviceId, ct);

            ProviderOutcome outcome = ClassifyOutcome(result.HttpStatusCode, result.Parsed?.Errno, result.TransportFailed, result.ParseFailed);

            FoxESSDeviceRealQueryDeviceResult? deviceResult = result.Parsed?.Result?
                .Find(candidate => string.Equals(candidate.DeviceSN, providerDeviceId, StringComparison.Ordinal));

            ProviderTelemetrySnapshot? snapshot = null;
            List<string>? returnedVariables = null;

            if (deviceResult != null)
            {
                List<ProviderReading> readings = new List<ProviderReading>();
                returnedVariables = new List<string>();

                foreach (FoxESSDeviceRealQueryDatum datum in deviceResult.Datas)
                {
                    returnedVariables.Add(datum.Variable);
                    readings.Add(new ProviderReading(datum.Variable, datum.Unit, datum.Value, ExtractChannel(datum.Variable)));
                }

                snapshot = new ProviderTelemetrySnapshot(
                    providerDeviceId,
                    DateTimeOffset.UtcNow,
                    deviceResult.Time,
                    TryParseProviderTimestamp(deviceResult.Time),
                    readings);
            }

            return new ProviderCallResult(
                outcome,
                null, // device status is not part of this call's response - the run-level discovery call owns it (SOL-T-501)
                (int?)result.HttpStatusCode,
                result.Parsed?.Errno,
                result.Parsed?.Msg,
                returnedVariables,
                result.RequestSanitized,
                result.RawResponseSanitized,
                snapshot);
        }

        private static string MapDeviceStatus(int status)
        {
            switch (status)
            {
                case 1:
                    return "Online";
                case 2:
                    return "Fault";
                case 3:
                    return "Offline";
                default:
                    return "Unknown";
            }
        }

        private static string? ExtractChannel(string variable)
        {
            Match match = PVChannelPattern.Match(variable);
            if (match.Success)
            {
                return match.Groups["channel"].Value;
            }

            return null;
        }

        private static DateTimeOffset? TryParseProviderTimestamp(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            // r04 §4.3: "yyyy-MM-dd HH:mm:ss zZ" - inverter local time with an offset. DateTimeOffset.TryParse
            // handles the common offset renderings FoxESS is likely to emit; a failure here is recorded as null
            // rather than thrown, matching SOL-T-704's "ObservedAtParseStatus" philosophy at the layer above.
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset parsed))
            {
                return parsed;
            }

            return null;
        }

        private static ProviderOutcome ClassifyOutcome(HttpStatusCode? httpStatus, int? errno, bool transportFailed, bool parseFailed)
        {
            if (transportFailed)
            {
                return ProviderOutcome.Transport;
            }

            if (parseFailed)
            {
                return ProviderOutcome.ParseFailure;
            }

            if (httpStatus == HttpStatusCode.Unauthorized || httpStatus == HttpStatusCode.Forbidden)
            {
                return ProviderOutcome.AuthFailed;
            }

            if (httpStatus.HasValue && (int)httpStatus.Value >= 500)
            {
                return ProviderOutcome.ProviderServerError;
            }

            if (errno == null || errno == 0)
            {
                return ProviderOutcome.Success;
            }

            switch (errno.Value)
            {
                case 40256:
                case 40257:
                    return ProviderOutcome.ValidationFailed;
                case 40400:
                    return ProviderOutcome.Throttled;
                default:
                    return ProviderOutcome.ProviderServerError;
            }
        }
    }
}
