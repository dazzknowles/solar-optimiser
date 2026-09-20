using System.Globalization;
using Microsoft.Extensions.Options;
using Sentry;
using SolarOptimiser.Domain;
using SolarOptimiser.Persistence.Abstract;
using SolarOptimiser.Persistence.Records;
using SolarOptimiser.Providers.Abstractions;

namespace SolarOptimiser.Collection
{
    /// <summary>
    /// One full poll: discover devices, fetch telemetry per device, classify quality, persist, record run/attempt
    /// evidence (SOL-T-501). Every device in a run is independent (SOL-T-803) - one device's unexpected failure
    /// never stops the others.
    /// </summary>
    public sealed class CollectionRunner : ICollectionRunner
    {
        private readonly ITelemetryProvider _telemetryProvider;
        private readonly ISiteRepository _siteRepository;
        private readonly IDeviceRepository _deviceRepository;
        private readonly IDeviceCapabilityRepository _deviceCapabilityRepository;
        private readonly ICollectionRunRepository _collectionRunRepository;
        private readonly ICaptureRepository _captureRepository;
        private readonly IOptions<CollectionOptions> _options;

        public CollectionRunner(
            ITelemetryProvider telemetryProvider,
            ISiteRepository siteRepository,
            IDeviceRepository deviceRepository,
            IDeviceCapabilityRepository deviceCapabilityRepository,
            ICollectionRunRepository collectionRunRepository,
            ICaptureRepository captureRepository,
            IOptions<CollectionOptions> options)
        {
            _telemetryProvider = telemetryProvider;
            _siteRepository = siteRepository;
            _deviceRepository = deviceRepository;
            _deviceCapabilityRepository = deviceCapabilityRepository;
            _collectionRunRepository = collectionRunRepository;
            _captureRepository = captureRepository;
            _options = options;
        }

        public async Task RunOnceAsync(CancellationToken cancellationToken)
        {
            DateTime startedAtUtc = DateTime.UtcNow;
            long collectionRunId = await _collectionRunRepository.StartAsync(startedAtUtc, cancellationToken);

            int devicesAttempted = 0;
            int devicesSucceeded = 0;
            int observationsWritten = 0;
            bool anyDiscoveryFailure = false;
            CollectionRunStatusEvidence? lastStatusEvidence = null;

            foreach (string providerSiteId in _options.Value.ProviderSiteIDs)
            {
                ProviderDiscoveryResult discovery = await _telemetryProvider.DiscoverDevicesAsync(providerSiteId, cancellationToken);

                lastStatusEvidence = new CollectionRunStatusEvidence(
                    DateTime.UtcNow,
                    discovery.HTTPStatus,
                    discovery.ProviderErrorNumber,
                    discovery.ProviderMessage,
                    null,
                    null);

                bool discoverySucceeded = discovery.Outcome == ProviderOutcome.Success && discovery.Devices != null;

                // No ProviderSite metadata (name/timezone) is exposed by ITelemetryProvider.DiscoverDevicesAsync as
                // specified (SOL-T-201) - only devices come back. The provider's own site ID is used as the name
                // fallback until a later spec revision exposes plant-level metadata.
                Site? site = await _siteRepository.GetByProviderIdAsync(_telemetryProvider.ProviderKey, providerSiteId, cancellationToken);

                if (discoverySucceeded)
                {
                    site = await _siteRepository.UpsertAsync(_telemetryProvider.ProviderKey, providerSiteId, providerSiteId, null, cancellationToken);

                    foreach (ProviderDevice providerDevice in discovery.Devices!)
                    {
                        await _deviceRepository.UpsertAsync(
                            site.ID,
                            providerDevice.ProviderDeviceID,
                            providerDevice.ModuleSerial,
                            providerDevice.Status,
                            providerDevice.Model,
                            providerDevice.HasPV,
                            providerDevice.HasBattery,
                            cancellationToken);
                    }
                }
                else
                {
                    anyDiscoveryFailure = true;
                }

                if (site == null)
                {
                    // Discovery failed on this process's very first ever run for this site: nothing is known yet,
                    // so there is genuinely nothing to attempt.
                    continue;
                }

                Dictionary<string, string> discoveryStatusByProviderDeviceId = discoverySucceeded
                    ? discovery.Devices!.ToDictionary(d => d.ProviderDeviceID, d => d.Status, StringComparer.Ordinal)
                    : new Dictionary<string, string>(StringComparer.Ordinal);

                IReadOnlyList<Device> devicesToAttempt = await _deviceRepository.GetBySiteAsync(site.ID, cancellationToken);

                foreach (Device device in devicesToAttempt)
                {
                    devicesAttempted++;

                    // The run's status call itself failing makes every device on that site untrustworthy this run,
                    // regardless of a device's previously stored status (SOL-T-601).
                    bool trusted = discoverySucceeded
                        && discoveryStatusByProviderDeviceId.TryGetValue(device.ProviderDeviceID, out string? discoveryStatus)
                        && discoveryStatus == "1";

                    string? statusForAttempt = discoverySucceeded && discoveryStatusByProviderDeviceId.TryGetValue(device.ProviderDeviceID, out string? knownStatus)
                        ? knownStatus
                        : null;

                    (bool succeeded, int written) = await ProcessDeviceAsync(collectionRunId, device, trusted, statusForAttempt, cancellationToken);

                    if (succeeded)
                    {
                        devicesSucceeded++;
                    }

                    observationsWritten += written;
                }
            }

            DateTime completedAtUtc = DateTime.UtcNow;

            string status;
            if (devicesAttempted == 0)
            {
                status = anyDiscoveryFailure ? "Failed" : "Success";
            }
            else if (devicesSucceeded == devicesAttempted)
            {
                status = "Success";
            }
            else if (devicesSucceeded > 0)
            {
                status = "PartialFailure";
            }
            else
            {
                status = "Failed";
            }

            await _collectionRunRepository.CompleteAsync(
                collectionRunId,
                completedAtUtc,
                devicesAttempted,
                devicesSucceeded,
                observationsWritten,
                status,
                lastStatusEvidence,
                cancellationToken);
        }

        private async Task<(bool Succeeded, int ObservationsWritten)> ProcessDeviceAsync(
            long collectionRunId,
            Device device,
            bool trusted,
            string? discoveryStatus,
            CancellationToken cancellationToken)
        {
            try
            {
                return await ProcessDeviceCoreAsync(collectionRunId, device, trusted, discoveryStatus, cancellationToken);
            }
            catch (Exception)
            {
                // One device's unexpected failure must never stop the run's remaining devices (SOL-T-803).
                return (false, 0);
            }
        }

        private async Task<(bool Succeeded, int ObservationsWritten)> ProcessDeviceCoreAsync(
            long collectionRunId,
            Device device,
            bool trusted,
            string? discoveryStatus,
            CancellationToken cancellationToken)
        {
            DateTime requestedAtUtc = DateTime.UtcNow;

            ProviderCallResult callResult = await CallWithRetryAsync(device.ProviderDeviceID, cancellationToken);

            DateTime completedAtUtc = DateTime.UtcNow;

            List<TelemetryObservationRecord> observations = new List<TelemetryObservationRecord>();

            string outcome;
            if (callResult.Outcome != ProviderOutcome.Success)
            {
                outcome = callResult.Outcome.ToString();
            }
            else if (!trusted)
            {
                outcome = nameof(ProviderOutcome.DeviceFaultOrOffline);
            }
            else
            {
                await BuildObservationsAsync(device, callResult, requestedAtUtc, observations, cancellationToken);
                bool anyNotOk = observations.Any(o => o.Quality != nameof(TelemetryQuality.Ok));
                outcome = anyNotOk ? nameof(ProviderOutcome.PartialMissing) : nameof(ProviderOutcome.Success);
            }

            (bool alertAttempted, string? sentryEventId) = AlertIfNeeded(device, outcome);
            if (alertAttempted)
            {
                // SOL-T-902: LastAlertedOutcome only advances once an alert genuinely fired - if Sentry itself
                // threw (SOL-T-904), it stays stale so the next poll retries alerting instead of silently
                // treating a failed notification as delivered.
                await _deviceRepository.UpdateLastAlertedOutcomeAsync(device.ID, outcome, cancellationToken);
            }

            CollectionAttemptRecord attempt = new CollectionAttemptRecord(
                collectionRunId,
                device.ID,
                requestedAtUtc,
                completedAtUtc,
                outcome,
                discoveryStatus,
                callResult.HTTPStatus,
                callResult.ProviderErrorNumber,
                callResult.ProviderMessage,
                null,
                callResult.ReturnedVariables != null ? string.Join(",", callResult.ReturnedVariables) : null,
                null,
                null,
                sentryEventId);

            await _captureRepository.RecordCaptureAsync(attempt, observations, cancellationToken);

            bool succeeded = outcome == nameof(ProviderOutcome.Success) || outcome == nameof(ProviderOutcome.PartialMissing);
            return (succeeded, observations.Count);
        }

        private async Task BuildObservationsAsync(
            Device device,
            ProviderCallResult callResult,
            DateTime requestedAtUtc,
            List<TelemetryObservationRecord> observations,
            CancellationToken cancellationToken)
        {
            if (callResult.Snapshot == null)
            {
                return;
            }

            IReadOnlyList<DeviceCapability> capabilities = await _deviceCapabilityRepository.GetByDeviceAsync(device.ID, cancellationToken);
            HashSet<string> returnedVariables = new HashSet<string>(StringComparer.Ordinal);

            foreach (ProviderReading reading in callResult.Snapshot.Readings)
            {
                returnedVariables.Add(reading.Variable);

                await _deviceCapabilityRepository.UpsertSeenAsync(device.ID, reading.Variable, reading.Unit, requestedAtUtc, cancellationToken);

                if (!FoxESSVariableMapping.TryMap(reading.Variable, out TelemetryQuantity quantity, out string? channel))
                {
                    continue;
                }

                string quality = reading.Value.HasValue ? nameof(TelemetryQuality.Ok) : nameof(TelemetryQuality.Invalid);

                string observedAtParseStatus;
                DateTime? observedAtUtc;
                if (callResult.Snapshot.ProviderTimestampParsed.HasValue)
                {
                    observedAtParseStatus = "Parsed";
                    observedAtUtc = callResult.Snapshot.ProviderTimestampParsed.Value.UtcDateTime;
                }
                else if (callResult.Snapshot.ProviderTimestampRaw != null)
                {
                    observedAtParseStatus = "Unparseable";
                    observedAtUtc = null;
                }
                else
                {
                    observedAtParseStatus = "NotAttempted";
                    observedAtUtc = null;
                }

                observations.Add(new TelemetryObservationRecord(
                    quantity.ToString(),
                    channel,
                    reading.Value,
                    reading.Unit,
                    quality,
                    reading.Variable,
                    FoxESSVariableMapping.MappingVersion,
                    callResult.Snapshot.ProviderTimestampRaw,
                    observedAtUtc,
                    observedAtParseStatus,
                    requestedAtUtc));
            }

            foreach (DeviceCapability capability in capabilities)
            {
                if (!capability.IsExpected || capability.RetiredAtUTC.HasValue)
                {
                    continue;
                }

                if (returnedVariables.Contains(capability.SourceVariable))
                {
                    continue;
                }

                if (!FoxESSVariableMapping.TryMap(capability.SourceVariable, out TelemetryQuantity quantity, out string? channel))
                {
                    continue;
                }

                observations.Add(new TelemetryObservationRecord(
                    quantity.ToString(),
                    channel,
                    null,
                    capability.Unit,
                    nameof(TelemetryQuality.Missing),
                    capability.SourceVariable,
                    FoxESSVariableMapping.MappingVersion,
                    null,
                    null,
                    "NotAttempted",
                    requestedAtUtc));
            }
        }

        private async Task<ProviderCallResult> CallWithRetryAsync(string providerDeviceId, CancellationToken cancellationToken)
        {
            ProviderCallResult result = await _telemetryProvider.GetLatestTelemetryAsync(providerDeviceId, cancellationToken);

            bool shouldRetry = result.Outcome == ProviderOutcome.Transport || result.Outcome == ProviderOutcome.ProviderServerError;
            if (!shouldRetry)
            {
                return result;
            }

            await Task.Delay(_options.Value.RetryDelay, cancellationToken);
            return await _telemetryProvider.GetLatestTelemetryAsync(providerDeviceId, cancellationToken);
        }

        private static (bool Attempted, string? SentryEventID) AlertIfNeeded(Device device, string outcome)
        {
            if (device.LastAlertedOutcome == outcome)
            {
                return (false, null);
            }

            bool isRecovery = outcome == nameof(ProviderOutcome.Success) && device.LastAlertedOutcome != null;
            bool isFailure = outcome != nameof(ProviderOutcome.Success);

            if (!isRecovery && !isFailure)
            {
                return (false, null);
            }

            try
            {
                SentrySdk.ConfigureScope(scope =>
                {
                    scope.SetTag("Outcome", isRecovery ? "Resolved" : outcome);
                    scope.SetTag("DeviceID", device.ID.ToString(CultureInfo.InvariantCulture));
                    scope.SetTag("SiteID", device.SiteID.ToString(CultureInfo.InvariantCulture));
                });

                string message = isRecovery
                    ? $"Device {device.ProviderDeviceID} recovered to Success"
                    : $"Device {device.ProviderDeviceID} outcome {outcome}";

                SentryLevel level = isRecovery ? SentryLevel.Info : SentryLevel.Error;

                SentryId sentryId = SentrySdk.CaptureMessage(message, level);
                // SentryId has no ToString("n") overload (unlike Guid) - strip the dashes from the default
                // dashed form to get the 32-character no-dash form Sentry's own UI/API use. SentryId.Empty
                // (no DSN configured - SOL-T-904) still counts as "attempted": the alert decision itself was
                // made and should be tracked, only a genuine SDK failure (below) must not be.
                string? sentryEventId = sentryId == SentryId.Empty ? null : sentryId.ToString().Replace("-", string.Empty);
                return (true, sentryEventId);
            }
            catch (Exception)
            {
                // Sentry/alerting is optional and must never block collection, and a failed notification must
                // not be mistaken for a delivered one (SOL-T-904).
                return (false, null);
            }
        }
    }
}
