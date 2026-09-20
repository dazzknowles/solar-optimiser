using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using SolarOptimiser.Collection;
using SolarOptimiser.Domain;
using SolarOptimiser.Host.Endpoints;
using SolarOptimiser.Persistence.Abstract;
using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Host.Tests;

[TestClass]
public sealed class TelemetryEndpointTests
{
    [TestMethod]
    public async Task Latest_ReturnsSeededRepositoryRows()
    {
        EndpointFixture fixture = new EndpointFixture();
        ObservationDetail observation = Observation(10, 20, "BatterySOC", null, 82m);
        fixture.Telemetry.GetLatestAsync(5, 42, "BatterySOC", Arg.Any<CancellationToken>())
            .Returns(new[] { new TelemetryLatestRecord(42, "Success", observation) });
        await using WebApplication app = await fixture.StartAsync();
        HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync("/api/sites/5/telemetry/latest?deviceId=42&quantity=BatterySOC");
        TelemetryLatestRecord[]? rows = await response.Content.ReadFromJsonAsync<TelemetryLatestRecord[]>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(rows);
        Assert.HasCount(1, rows);
        Assert.AreEqual(82m, rows[0].Observation.ValueParsed);
        Assert.AreEqual(20L, rows[0].Observation.CaptureID);
    }

    [TestMethod]
    public async Task RangedQuery_ClampsLimitAndForwardsFiltersAndCursor()
    {
        EndpointFixture fixture = new EndpointFixture();
        fixture.Devices.GetBySiteAsync(5, Arg.Any<CancellationToken>()).Returns(new[] { Device(42, 5) });
        fixture.Telemetry.QueryAsync(Arg.Any<TelemetryQueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TelemetryQueryPage(new[] { Observation(11, 20, "PVStringPower", "3", 1.5m) }, "next-token"));
        await using WebApplication app = await fixture.StartAsync();
        HttpClient client = app.GetTestClient();

        HttpResponseMessage response = await client.GetAsync(
            "/api/sites/5/telemetry?deviceId=42&quantity=PVStringPower&channel=3&from=2026-09-20T10:00:00Z&to=2026-09-20T11:00:00Z&limit=9999&cursor=abc");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        await fixture.Telemetry.Received(1).QueryAsync(
            Arg.Is<TelemetryQueryRequest>(request =>
                request.DeviceID == 42
                && request.Quantity == "PVStringPower"
                && request.Channel == "3"
                && request.From == new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc)
                && request.To == new DateTime(2026, 9, 20, 11, 0, 0, DateTimeKind.Utc)
                && request.Limit == 5000
                && request.Cursor == "abc"),
            Arg.Any<CancellationToken>());
        TelemetryQueryPage? page = await response.Content.ReadFromJsonAsync<TelemetryQueryPage>();
        Assert.AreEqual("next-token", page!.NextCursor);
    }

    [TestMethod]
    public async Task RangedQuery_DeviceOutsideSite_ReturnsNotFound()
    {
        EndpointFixture fixture = new EndpointFixture();
        fixture.Devices.GetBySiteAsync(5, Arg.Any<CancellationToken>()).Returns(new[] { Device(41, 5) });
        await using WebApplication app = await fixture.StartAsync();

        HttpResponseMessage response = await app.GetTestClient().GetAsync("/api/sites/5/telemetry?deviceId=42");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        await fixture.Telemetry.DidNotReceiveWithAnyArgs().QueryAsync(default!, default);
    }

    [TestMethod]
    public async Task RangedQuery_MultipleDevicesWithoutDeviceId_ReturnsBadRequest()
    {
        EndpointFixture fixture = new EndpointFixture();
        fixture.Devices.GetBySiteAsync(5, Arg.Any<CancellationToken>()).Returns(new[] { Device(41, 5), Device(42, 5) });
        await using WebApplication app = await fixture.StartAsync();

        HttpResponseMessage response = await app.GetTestClient().GetAsync("/api/sites/5/telemetry");

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Nearest_UsesThreePollIntervalsByDefaultAndReturnsSignedDifference()
    {
        EndpointFixture fixture = new EndpointFixture();
        DateTime requestedAt = new DateTime(2026, 9, 20, 10, 7, 0, DateTimeKind.Utc);
        CaptureDetail capture = new CaptureDetail(20, 3, 42, requestedAt, requestedAt, "Success", "Online", null, "SoC", new[] { Observation(10, 20, "BatterySOC", null, 82m) });
        fixture.Telemetry.GetNearestCaptureAsync(42, Arg.Any<DateTime>(), TimeSpan.FromMinutes(15), Arg.Any<CancellationToken>())
            .Returns(new NearestCaptureResult(capture, TimeSpan.FromMinutes(2)));
        await using WebApplication app = await fixture.StartAsync();

        HttpResponseMessage response = await app.GetTestClient().GetAsync("/api/devices/42/captures/nearest?at=2026-09-20T10:05:00Z");
        NearestCaptureResult? result = await response.Content.ReadFromJsonAsync<NearestCaptureResult>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual(TimeSpan.FromMinutes(2), result!.SignedTimeDifference);
        Assert.AreEqual(20L, result.Capture.CaptureID);
    }

    [TestMethod]
    public async Task CaptureDetail_ReturnsAllContemporaneousObservations()
    {
        EndpointFixture fixture = new EndpointFixture();
        DateTime requestedAt = new DateTime(2026, 9, 20, 10, 7, 0, DateTimeKind.Utc);
        CaptureDetail capture = new CaptureDetail(
            20, 3, 42, requestedAt, requestedAt, "PartialMissing", "Online", null, "SoC,pvPower",
            new[] { Observation(10, 20, "BatterySOC", null, 82m), Observation(11, 20, "PVPowerTotal", null, null) });
        fixture.Telemetry.GetCaptureAsync(20, Arg.Any<CancellationToken>()).Returns(capture);
        await using WebApplication app = await fixture.StartAsync();

        HttpResponseMessage response = await app.GetTestClient().GetAsync("/api/captures/20");
        CaptureDetail? result = await response.Content.ReadFromJsonAsync<CaptureDetail>();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.IsNotNull(result);
        Assert.HasCount(2, result.Observations);
        Assert.AreEqual("PartialMissing", result.Outcome);
    }

    private static Device Device(long id, long siteId)
    {
        return new Device { ID = id, SiteID = siteId, ProviderDeviceID = $"DEVICE-{id}", Status = "Online" };
    }

    private static ObservationDetail Observation(long id, long captureId, string quantity, string? channel, decimal? value)
    {
        return new ObservationDetail(
            id, captureId, quantity, channel, value, "kW", value.HasValue ? "Ok" : "Missing", "fixture", 1,
            null, null, "NotAttempted", new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc));
    }

    private sealed class EndpointFixture
    {
        public EndpointFixture()
        {
            Devices = Substitute.For<IDeviceRepository>();
            Telemetry = Substitute.For<ITelemetryQueryRepository>();
        }

        public IDeviceRepository Devices { get; }

        public ITelemetryQueryRepository Telemetry { get; }

        public async Task<WebApplication> StartAsync()
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton(Devices);
            builder.Services.AddSingleton(Telemetry);
            builder.Services.AddSingleton<IOptions<CollectionOptions>>(Options.Create(new CollectionOptions
            {
                PollInterval = TimeSpan.FromMinutes(5)
            }));
            WebApplication app = builder.Build();
            app.MapTelemetryEndpoints();
            await app.StartAsync();
            return app;
        }
    }
}
