using System.Data;
using System.Data.Common;
using System.Diagnostics;
using Eceni.Core.Base.Database.Abstract;
using Eceni.Core.Base.Database.Concrete;
using Eceni.Core.Database.MySQL;
using MySqlConnector;
using NSubstitute;
using SolarOptimiser.Domain;
using SolarOptimiser.Persistence.Records;
using SolarOptimiser.Persistence.Repositories;

namespace SolarOptimiser.Persistence.Tests;

[TestClass]
[DoNotParallelize]
public sealed class MariaDbPersistenceTests
{
    private const string ConnectionVariable = "SOLAR_TEST_MYSQL_CONNECTION_STRING";

    private static string ConnectionString => Environment.GetEnvironmentVariable(ConnectionVariable) ?? string.Empty;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        RequireDatabase();
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
    public async Task SchemaAndStoredProcedures_SupportRepositoryUnitOfWorkRoundTrip()
    {
        (long runId, Device device) = await SeedRunAndDeviceAsync();
        CaptureRepository repository = CreateCaptureRepository();
        DateTime retrievedAt = new DateTime(2026, 9, 20, 10, 30, 0, DateTimeKind.Utc);
        CollectionAttemptRecord attempt = Attempt(runId, device.ID, retrievedAt);
        TelemetryObservationRecord[] observations =
        {
            Observation("BatterySOC", "SoC", 71.25m, retrievedAt),
            Observation("PVStringPower", "pv3Power", 2.125m, retrievedAt, "3")
        };

        long captureId = await repository.RecordCaptureAsync(attempt, observations, CancellationToken.None);

        Assert.IsGreaterThan(0L, captureId);
        Assert.AreEqual(1L, await ScalarAsync<long>("SELECT COUNT(*) FROM CollectionAttempts WHERE ID = @id", new MySqlParameter("@id", captureId)));
        Assert.AreEqual(2L, await ScalarAsync<long>("SELECT COUNT(*) FROM TelemetryObservations WHERE CaptureID = @id", new MySqlParameter("@id", captureId)));
        Assert.AreEqual(0L, await ScalarAsync<long>("SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'TelemetryObservations' AND COLUMN_NAME = 'DeviceID'"));
    }

    [TestMethod]
    public async Task RecordCapture_WhenSecondObservationCommandFails_RollsBackAttemptAndFirstObservation()
    {
        (long runId, Device device) = await SeedRunAndDeviceAsync();
        CaptureRepository repository = CreateCaptureRepository();
        DateTime requestedAt = new DateTime(2026, 9, 20, 11, 0, 0, DateTimeKind.Utc);
        CollectionAttemptRecord attempt = Attempt(runId, device.ID, requestedAt);
        TelemetryObservationRecord[] observations =
        {
            Observation("BatterySOC", "SoC", 50m, requestedAt),
            Observation("PVPowerTotal", null!, 1m, requestedAt)
        };

        await Assert.ThrowsAsync<MySqlException>(
            () => repository.RecordCaptureAsync(attempt, observations, CancellationToken.None));

        Assert.AreEqual(0L, await ScalarAsync<long>(
            "SELECT COUNT(*) FROM CollectionAttempts WHERE CollectionRunID = @runId AND RequestedAtUTC = @requestedAt",
            new MySqlParameter("@runId", runId),
            new MySqlParameter("@requestedAt", requestedAt)));
        Assert.AreEqual(0L, await ScalarAsync<long>("SELECT COUNT(*) FROM TelemetryObservations"));
    }

    [TestMethod]
    public async Task EstimatedDailyVolume_6900Rows_InsertsWithinBasicPerformanceBound()
    {
        (long runId, Device device) = await SeedRunAndDeviceAsync();
        CaptureRepository repository = CreateCaptureRepository();
        DateTime requestedAt = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        List<TelemetryObservationRecord> observations = new List<TelemetryObservationRecord>(6900);
        for (int index = 0; index < 6900; index++)
        {
            observations.Add(Observation("BatterySOC", "SoC", index % 101, requestedAt));
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        long captureId = await repository.RecordCaptureAsync(Attempt(runId, device.ID, requestedAt), observations, CancellationToken.None);
        stopwatch.Stop();

        Assert.AreEqual(6900L, await ScalarAsync<long>("SELECT COUNT(*) FROM TelemetryObservations WHERE CaptureID = @id", new MySqlParameter("@id", captureId)));
        Assert.IsLessThan(TimeSpan.FromMinutes(2), stopwatch.Elapsed,
            $"The estimated daily volume took {stopwatch.Elapsed}; the basic integration-volume check allows two minutes.");
    }

    [TestMethod]
    public async Task GetLatest_OrdersByRetrievedTimestampBeforeInsertionId()
    {
        (long runId, Device device) = await SeedRunAndDeviceAsync();
        CaptureRepository captures = CreateCaptureRepository();
        DateTime newer = new DateTime(2026, 9, 20, 14, 0, 0, DateTimeKind.Utc);
        DateTime older = newer.AddHours(-1);
        long newerCaptureId = await captures.RecordCaptureAsync(
            Attempt(runId, device.ID, newer),
            new[] { Observation("BatterySOC", "SoC", 80m, newer) },
            CancellationToken.None);
        await captures.RecordCaptureAsync(
            Attempt(runId, device.ID, older),
            new[] { Observation("BatterySOC", "SoC", 20m, older) },
            CancellationToken.None);
        TelemetryQueryRepository queries = new TelemetryQueryRepository(CreateResolver());

        IReadOnlyList<TelemetryLatestRecord> latest = await queries.GetLatestAsync(device.SiteID, device.ID, "BatterySOC", CancellationToken.None);

        Assert.HasCount(1, latest);
        Assert.AreEqual(newerCaptureId, latest[0].Observation.CaptureID,
            "Latest is defined by RetrievedAtUTC DESC, ID DESC, not by whichever row was inserted last.");
        Assert.AreEqual(80m, latest[0].Observation.ValueParsed);
    }

    private static async Task<(long RunID, Device Device)> SeedRunAndDeviceAsync()
    {
        IDBConnectionResolver resolver = CreateResolver();
        SiteRepository sites = new SiteRepository(resolver);
        DeviceRepository devices = new DeviceRepository(resolver);
        CollectionRunRepository runs = new CollectionRunRepository(resolver);
        Site site = await sites.UpsertAsync("FoxESS", "SITE-TEST", "Test site", "Europe/London", CancellationToken.None);
        Device device = await devices.UpsertAsync(site.ID, "DEVICE-TEST", "LOGGER", "Online", "H3", true, true, CancellationToken.None);
        long runId = await runs.StartAsync(DateTime.UtcNow, CancellationToken.None);
        return (runId, device);
    }

    private static CaptureRepository CreateCaptureRepository()
    {
        return new CaptureRepository(CreateResolver());
    }

    private static IDBConnectionResolver CreateResolver()
    {
        IDBConnectionResolver resolver = Substitute.For<IDBConnectionResolver>();
        resolver.ResolveConnectionString(Arg.Any<string?>()).Returns(ConnectionString);
        resolver.ResolveProvider(Arg.Any<string?>()).Returns(DBUtilityMySQL.ProviderName);
        return resolver;
    }

    private static CollectionAttemptRecord Attempt(long runId, long deviceId, DateTime requestedAt)
    {
        return new CollectionAttemptRecord(
            runId, deviceId, requestedAt, requestedAt.AddSeconds(1), "Success", "Online", 200, 0, "ok",
            null, "SoC", null, null, null);
    }

    private static TelemetryObservationRecord Observation(
        string quantity,
        string sourceVariable,
        decimal value,
        DateTime retrievedAt,
        string? channel = null)
    {
        return new TelemetryObservationRecord(
            quantity, channel, value, "%", "Ok", sourceVariable, 1,
            "2026-09-20 12:00:00 +00:00", retrievedAt, "Parsed", retrievedAt);
    }

    private static void RequireDatabase()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Assert.Inconclusive($"Set {ConnectionVariable} to run MariaDB integration tests.");
        }
    }

    private static async Task ResetDatabaseAsync()
    {
        await DropSchemaObjectsAsync();
        string repositoryRoot = FindRepositoryRoot();
        await ExecuteScriptAsync(await File.ReadAllTextAsync(Path.Combine(repositoryRoot, "src", "SolarOptimiser.Persistence", "Database", "Schema.sql")));
        string proceduresPath = Path.Combine(repositoryRoot, "src", "SolarOptimiser.Persistence", "Database", "StoredProcedures");
        foreach (string path in Directory.GetFiles(proceduresPath, "*.sql").OrderBy(item => item, StringComparer.Ordinal))
        {
            await ExecuteScriptAsync(await File.ReadAllTextAsync(path));
        }
    }

    private static async Task DropSchemaObjectsAsync()
    {
        string[] procedureNames =
        {
            "espCollectionAttemptGetByID", "espCollectionAttemptGetNearest", "espCollectionAttemptInsert",
            "espCollectionRunComplete", "espCollectionRunStart", "espDeviceBatteryUpsert",
            "espDeviceCapabilityApprove", "espDeviceCapabilityGetByDevice", "espDeviceCapabilityRetire",
            "espDeviceCapabilityUpsert", "espDeviceGetByID", "espDeviceGetBySite", "espDeviceUpdateLastAlertedOutcome",
            "espDeviceUpsert", "espSiteGetByProviderID", "espSiteUpsert", "espTelemetryObservationGetByCapture",
            "espTelemetryObservationGetLatest", "espTelemetryObservationInsert", "espTelemetryObservationQuery"
        };
        List<string> statements = new List<string>();
        foreach (string procedureName in procedureNames)
        {
            statements.Add($"DROP PROCEDURE IF EXISTS {procedureName}");
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

    private static async Task<T> ScalarAsync<T>(string sql, params MySqlParameter[] parameters)
    {
        await using MySqlConnection connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using MySqlCommand command = new MySqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        object? result = await command.ExecuteScalarAsync();
        return (T)Convert.ChangeType(result!, typeof(T));
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
