using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using SolarOptimiser.Collection;
using SolarOptimiser.Domain;
using SolarOptimiser.Persistence.Abstract;
using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Host.Endpoints
{
    /// <summary>
    /// The retrieval API: SOL-T-1001 (latest), SOL-T-1002 (ranged/paged), SOL-T-1003 (nearest-to-a-timestamp),
    /// SOL-T-1004 (capture by ID). <see cref="ITelemetryQueryRepository"/>'s request/response shapes have no
    /// site scoping of their own (a device belongs to exactly one site already), so the <c>{siteId}</c>-scoped
    /// routes below resolve the site's device(s) via <see cref="IDeviceRepository"/> first.
    /// </summary>
    public static class TelemetryEndpoints
    {
        public static IEndpointRouteBuilder MapTelemetryEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapGet("/api/sites/{siteId:long}/telemetry/latest", GetLatestAsync);
            app.MapGet("/api/sites/{siteId:long}/telemetry", QueryAsync);
            app.MapGet("/api/devices/{deviceId:long}/captures/nearest", GetNearestCaptureAsync);
            app.MapGet("/api/captures/{captureId:long}", GetCaptureAsync);

            return app;
        }

        private static async Task<IResult> GetLatestAsync(
            long siteId,
            long? deviceId,
            string? quantity,
            ITelemetryQueryRepository telemetryQueryRepository,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<TelemetryLatestRecord> latest = await telemetryQueryRepository.GetLatestAsync(siteId, deviceId, quantity, cancellationToken);
            return Results.Ok(latest);
        }

        private static async Task<IResult> QueryAsync(
            long siteId,
            long? deviceId,
            string? quantity,
            string? channel,
            DateTime? from,
            DateTime? to,
            int? limit,
            string? cursor,
            IDeviceRepository deviceRepository,
            ITelemetryQueryRepository telemetryQueryRepository,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<Device> devices = await deviceRepository.GetBySiteAsync(siteId, cancellationToken);
            long resolvedDeviceId;

            if (deviceId.HasValue)
            {
                bool belongsToSite = false;
                foreach (Device device in devices)
                {
                    if (device.ID == deviceId.Value)
                    {
                        belongsToSite = true;
                        break;
                    }
                }

                if (!belongsToSite)
                {
                    return Results.NotFound($"Device {deviceId} was not found at site {siteId}.");
                }

                resolvedDeviceId = deviceId.Value;
            }
            else if (devices.Count == 1)
            {
                resolvedDeviceId = devices[0].ID;
            }
            else
            {
                return Results.BadRequest("This site has more than one device - pass deviceId to disambiguate.");
            }

            int effectiveLimit = limit ?? 500;
            if (effectiveLimit > 5000)
            {
                effectiveLimit = 5000;
            }

            TelemetryQueryRequest request = new TelemetryQueryRequest(resolvedDeviceId, quantity, channel, from, to, effectiveLimit, cursor);
            TelemetryQueryPage page = await telemetryQueryRepository.QueryAsync(request, cancellationToken);
            return Results.Ok(page);
        }

        private static async Task<IResult> GetNearestCaptureAsync(
            long deviceId,
            DateTime at,
            int? maxDistanceSeconds,
            ITelemetryQueryRepository telemetryQueryRepository,
            IOptions<CollectionOptions> collectionOptions,
            CancellationToken cancellationToken)
        {
            TimeSpan defaultMaxDistance = TimeSpan.FromTicks(collectionOptions.Value.PollInterval.Ticks * 3);
            TimeSpan maxDistance = maxDistanceSeconds.HasValue
                ? TimeSpan.FromSeconds(maxDistanceSeconds.Value)
                : defaultMaxDistance;

            NearestCaptureResult? result = await telemetryQueryRepository.GetNearestCaptureAsync(deviceId, at, maxDistance, cancellationToken);
            if (result == null)
            {
                return Results.NotFound("No suitable capture was found within the configured maximum distance.");
            }

            return Results.Ok(result);
        }

        private static async Task<IResult> GetCaptureAsync(
            long captureId,
            ITelemetryQueryRepository telemetryQueryRepository,
            CancellationToken cancellationToken)
        {
            CaptureDetail? capture = await telemetryQueryRepository.GetCaptureAsync(captureId, cancellationToken);
            if (capture == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(capture);
        }
    }
}
