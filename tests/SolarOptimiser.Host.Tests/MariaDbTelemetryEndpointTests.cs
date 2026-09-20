using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Database.MySQL;
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
using SolarOptimiser.Persistence.Repositories;

namespace SolarOptimiser.Host.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MariaDbTelemetryEndpointTests
{
    private const string ConnectionVariable = "SOLAR_TEST_MYSQL_CONNECTION_STRING";

    private static string ConnectionString => Environment.GetEnvironmentVariable(ConnectionVariable) ?? string.Empty;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Assert.Inconclusive($"Set {ConnectionVariable} to run retrieval endpoint tests against MariaDB.");
        }

        await ResetDatabaseAsync();
    }

    [TestCleanup]
    public async Task CleanupAsync()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            await DropSchemaObjectsAsync();
        }
    }

    [TestMethod]
    public async Task RetrievalEndpoints_ReturnSeededRowsWithStablePagingLatestNearestAndDetail()
    {
        IDBConnectionResolver resolver = CreateResolver();
        SiteRepository sites = new SiteRepository(resolver);
        DeviceRepository devices = new DeviceRepository(resolver);
        CollectionRunRepository runs = new CollectionRunRepository(resolver);
        CaptureRepository captures = new CaptureRepository(resolver);
        TelemetryQueryRepository telemetry = new TelemetryQueryRepository(resolver);
        Site site = await sites.UpsertAsync("FoxESS", "SITE-HOST", "Host test", "Europe/London", CancellationToken.None);
        Device device = await devices.UpsertAsync(site.ID, "DEVICE-HOST", null, "Online", "H3", true, true, CancellationToken.None);
        long runId = await runs.StartAsync(new DateTime(2026, 9, 20, 9, 55, 0, DateTimeKind.Utc), CancellationToken.None);
        DateTime firstTime = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
        DateTime secondTime = new DateTime(2026, 9, 20, 10, 10, 0, DateTimeKind.Utc);
        long firstCaptureId = await captures.RecordCaptureAsync(
            Attempt(runId, device.ID, firstTime, "SoC"),
            new[] { Observation("BatterySOC", "SoC", 40m, firstTime) },
            CancellationToken.None);
        long secondCaptureId = await captures.RecordCaptureAsync(
            Attempt(runId, device.ID, secondTime, "SoC,pvPower"),
            new[]
            {
                Observation("BatterySOC", "SoC", 55m, secondTime),
                Observation("PVPowerTotal", "pvPower", 2.25m, secondTime)
            },
            CancellationToken.None);

        await using WebApplication app = await StartAppAsync(devices, telemetry);
        HttpClient client = app.GetTestClient();

        TelemetryLatestRecord[]? latest = await client.GetFromJsonAsync<TelemetryLatestRecord[]>(
            $"/api/sites/{site.ID}/telemetry/latest?deviceId={device.ID}");
        Assert.IsNotNull(latest);
        Assert.HasCount(2, latest);
        Assert.AreEqual(secondCaptureId, latest.Single(item => item.Observation.Quantity == "BatterySOC").Observation.CaptureID);
        Assert.AreEqual(55m, latest.Single(item => item.Observation.Quantity == "BatterySOC").Observation.ValueParsed);

        string firstPageUrl = $"/api/sites/{site.ID}/telemetry?deviceId={device.ID}&from=2026-09-20T09:59:00Z&to=2026-09-20T10:11:00Z&limit=1";
        TelemetryQueryPage? firstPage = await client.GetFromJsonAsync<TelemetryQueryPage>(firstPageUrl);
        Assert.IsNotNull(firstPage);
        Assert.HasCount(1, firstPage.Items);
        Assert.AreEqual(firstCaptureId, firstPage.Items[0].CaptureID);
        Assert.IsNotNull(firstPage.NextCursor);

        string secondPageUrl = firstPageUrl + "&cursor=" + Uri.EscapeDataString(firstPage.NextCursor);
        TelemetryQueryPage? secondPage = await client.GetFromJsonAsync<TelemetryQueryPage>(secondPageUrl);
        Assert.IsNotNull(secondPage);
        Assert.HasCount(1, secondPage.Items);
        Assert.AreEqual(secondCaptureId, secondPage.Items[0].CaptureID);
        Assert.IsGreaterThan(firstPage.Items[0].ID, secondPage.Items[0].ID);

        NearestCaptureResult? nearest = await client.GetFromJsonAsync<NearestCaptureResult>(
            $"/api/devices/{device.ID}/captures/nearest?at=2026-09-20T10:05:00Z&maxDistanceSeconds=301");
        Assert.IsNotNull(nearest);
        Assert.AreEqual(secondCaptureId, nearest.Capture.CaptureID);
        Assert.AreEqual(TimeSpan.FromMinutes(5), nearest.SignedTimeDifference);

        CaptureDetail? detail = await client.GetFromJsonAsync<CaptureDetail>($"/api/captures/{secondCaptureId}");
        Assert.IsNotNull(detail);
        Assert.AreEqual("SoC,pvPower", detail.ReturnedVariables);
        Assert.HasCount(2, detail.Observations);

        HttpResponseMessage missing = await client.GetAsync($"/api/captures/{secondCaptureId + 1000}");
        Assert.AreEqual(HttpStatusCode.NotFound, missing.StatusCode);
    }

    private static async Task<WebApplication> StartAppAsync(IDeviceRepository devices, ITelemetryQueryRepository telemetry)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton(devices);
        builder.Services.AddSingleton(telemetry);
        builder.Services.AddSingleton<IOptions<CollectionOptions>>(Options.Create(new CollectionOptions
        {
            PollInterval = TimeSpan.FromMinutes(5)
        }));
        WebApplication app = builder.Build();
        app.MapTelemetryEndpoints();
        await app.StartAsync();
        return app;
    }

    private static CollectionAttemptRecord Attempt(long runId, long deviceId, DateTime requestedAt, string returnedVariables)
    {
        return new CollectionAttemptRecord(
            runId, deviceId, requestedAt, requestedAt.AddSeconds(1), "Success", "Online", 200, 0, "ok",
            null, returnedVariables, null, null, null);
    }

    private static TelemetryObservationRecord Observation(string quantity, string sourceVariable, decimal value, DateTime retrievedAt)
    {
        return new TelemetryObservationRecord(
            quantity, null, value, "kW", "Ok", sourceVariable, 1,
            null, null, "NotAttempted", retrievedAt);
    }

    private static IDBConnectionResolver CreateResolver()
    {
        IDBConnectionResolver resolver = Substitute.For<IDBConnectionResolver>();
        resolver.ResolveConnectionString(Arg.Any<string?>()).Returns(ConnectionString);
        resolver.ResolveProvider(Arg.Any<string?>()).Returns(DBUtilityMySQL.ProviderName);
        return resolver;
    }

    private static async Task ResetDatabaseAsync()
    {
        await DropSchemaObjectsAsync();
        string root = FindRepositoryRoot();
        await ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(root, "src", "SolarOptimiser.Persistence", "Database", "Schema.sql")));
        string procedures = Path.Combine(root, "src", "SolarOptimiser.Persistence", "Database", "StoredProcedures");
        foreach (string path in Directory.GetFiles(procedures, "*.sql").OrderBy(item => item, StringComparer.Ordinal))
        {
            await ExecuteScriptAsync(await File.ReadAllTextAsync(path));
        }
    }

    private static async Task DropSchemaObjectsAsync()
    {
        string[] procedures =
        {
            "espCollectionAttemptGetByID", "espCollectionAttemptGetNearest", "espCollectionAttemptInsert",
            "espCollectionRunComplete", "espCollectionRunStart", "espDeviceBatteryUpsert",
            "espDeviceCapabilityApprove", "espDeviceCapabilityGetByDevice", "espDeviceCapabilityRetire",
            "espDeviceCapabilityUpsert", "espDeviceGetByID", "espDeviceGetBySite", "espDeviceUpdateLastAlertedOutcome",
            "espDeviceUpsert", "espSiteGetByProviderID", "espSiteUpsert", "espTelemetryObservationGetByCapture",
            "espTelemetryObservationGetLatest", "espTelemetryObservationInsert", "espTelemetryObservationQuery"
        };
        List<string> statements = new List<string>();
        foreach (string procedure in procedures)
        {
            statements.Add($"DROP PROCEDURE IF EXISTS {procedure}");
        }

        statements.Add("SET FOREIGN_KEY_CHECKS = 0");
        statements.Add("DROP TABLE IF EXISTS TelemetryObservations, CollectionAttempts, CollectionRuns, DeviceBatteries, DeviceCapabilities, Devices, Sites");
        statements.Add("SET FOREIGN_KEY_CHECKS = 1");
        await ExecuteScriptAsync(string.Join(";\n", statements) + ";");
    }

    private static async Task ExecuteScriptAsync(string script)
    {
        DBUtilityMySQL utility = new DBUtilityMySQL();
        DbConnection connection = utility.CreateConnectionAsync(ConnectionString);
        await foreach (int _ in utility.ExecuteScriptAsync(connection, script))
        {
        }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SolarOptimiser.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate SolarOptimiser.sln from the test output directory.");
    }
}
