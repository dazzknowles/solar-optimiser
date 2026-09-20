using Microsoft.Extensions.Options;
using NSubstitute;
using SolarOptimiser.Domain;
using SolarOptimiser.Persistence.Abstract;
using SolarOptimiser.Persistence.Records;
using SolarOptimiser.Providers.Abstractions;

namespace SolarOptimiser.Collection.Tests;

[TestClass]
public sealed class CollectionRunnerTests
{
    [TestMethod]
    public async Task TrustedSuccess_WritesPresentValidInvalidAndExpectedMissingRowsAdditively()
    {
        RunnerFixture fixture = new RunnerFixture("1");
        fixture.Capabilities = new[]
        {
            Capability("loadsPower", true),
            Capability("generation", false),
            Capability("meterPower", true, DateTime.UtcNow)
        };
        fixture.TelemetryResults.Enqueue(Result(
            ProviderOutcome.Success,
            new ProviderReading("SoC", "%", 87.2m, null),
            new ProviderReading("pvPower", "kW", null, null),
            new ProviderReading("unmapped", "widgets", 9m, null)));

        await fixture.RunAsync();

        IReadOnlyList<TelemetryObservationRecord> observations = fixture.SingleCaptureObservations();
        Assert.HasCount(3, observations);
        AssertObservation(observations, "SoC", "BatterySOC", "Ok", 87.2m);
        AssertObservation(observations, "pvPower", "PVPowerTotal", "Invalid", null);
        AssertObservation(observations, "loadsPower", "LoadPower", "Missing", null);
        Assert.IsFalse(observations.Any(item => item.SourceVariable == "generation"));
        Assert.IsFalse(observations.Any(item => item.SourceVariable == "meterPower"));
        await fixture.CapabilityRepository.Received(1).UpsertSeenAsync(42, "unmapped", "widgets", Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
        Assert.AreEqual("PartialMissing", fixture.SingleAttempt().Outcome);
    }

    [TestMethod]
    public async Task FullSuccess_WritesAllRowsAsOk()
    {
        RunnerFixture fixture = new RunnerFixture("1");
        fixture.TelemetryResults.Enqueue(Result(
            ProviderOutcome.Success,
            new ProviderReading("SoC", "%", 60m, null),
            new ProviderReading("pv2Power", "kW", 2.4m, "2")));

        await fixture.RunAsync();

        IReadOnlyList<TelemetryObservationRecord> observations = fixture.SingleCaptureObservations();
        Assert.HasCount(2, observations);
        Assert.IsTrue(observations.All(item => item.Quality == "Ok"));
        Assert.AreEqual("2", observations.Single(item => item.SourceVariable == "pv2Power").Channel);
        Assert.AreEqual("Success", fixture.SingleAttempt().Outcome);
    }

    [TestMethod]
    public async Task WholeProviderFailure_WritesAttemptButNoObservations()
    {
        RunnerFixture fixture = new RunnerFixture("1");
        fixture.TelemetryResults.Enqueue(Result(ProviderOutcome.AuthFailed, new ProviderReading("SoC", "%", 60m, null)));

        await fixture.RunAsync();

        Assert.HasCount(0, fixture.SingleCaptureObservations());
        Assert.AreEqual("AuthFailed", fixture.SingleAttempt().Outcome);
    }

    [TestMethod]
    [DataRow("2")]
    [DataRow("3")]
    [DataRow("Unknown")]
    public async Task UntrustedDeviceStatus_SuppressesAllObservationRows(string status)
    {
        RunnerFixture fixture = new RunnerFixture(status);
        fixture.TelemetryResults.Enqueue(Result(ProviderOutcome.Success, new ProviderReading("SoC", "%", 60m, null)));

        await fixture.RunAsync();

        Assert.HasCount(0, fixture.SingleCaptureObservations());
        Assert.AreEqual("DeviceFaultOrOffline", fixture.SingleAttempt().Outcome);
        Assert.AreEqual(status, fixture.SingleAttempt().DeviceStatus);
    }

    [TestMethod]
    public async Task FailedDiscoveryForKnownSite_SuppressesTelemetryEvenWhenStoredStatusWasOnline()
    {
        RunnerFixture fixture = new RunnerFixture("1");
        fixture.Discovery = new ProviderDiscoveryResult(
            ProviderOutcome.Transport, null, null, "down", null, null, null);
        fixture.TelemetryResults.Enqueue(Result(ProviderOutcome.Success, new ProviderReading("SoC", "%", 60m, null)));

        await fixture.RunAsync();

        Assert.HasCount(0, fixture.SingleCaptureObservations());
        Assert.AreEqual("DeviceFaultOrOffline", fixture.SingleAttempt().Outcome);
        Assert.IsNull(fixture.SingleAttempt().DeviceStatus);
    }

    [TestMethod]
    public async Task SanitizedProviderEvidence_IsRecordedToPathsOwnedByTheAttempt()
    {
        RunnerFixture fixture = new RunnerFixture("1");
        ProviderTelemetrySnapshot snapshot = new ProviderTelemetrySnapshot(
            "DEVICE-1",
            DateTimeOffset.UtcNow,
            null,
            null,
            new[] { new ProviderReading("SoC", "%", 60m, null) });
        fixture.TelemetryResults.Enqueue(new ProviderCallResult(
            ProviderOutcome.Success,
            null,
            200,
            0,
            "ok",
            new[] { "SoC" },
            "POST /op/v1/device/real/query",
            "{\"errno\":0}",
            snapshot));

        await fixture.RunAsync();

        CollectionAttemptRecord attempt = fixture.SingleAttempt();
        Assert.IsNotNull(attempt.RequestPath,
            "Successful best-effort evidence persistence should record the request file path on its attempt.");
        Assert.IsNotNull(attempt.RawResponsePath,
            "Successful best-effort evidence persistence should record the response file path on its attempt.");
    }

    [TestMethod]
    public async Task SentryUnavailable_DoesNotAdvanceLastAlertedOutcome()
    {
        RunnerFixture fixture = new RunnerFixture("1");
        fixture.TelemetryResults.Enqueue(Result(ProviderOutcome.AuthFailed));

        await fixture.RunAsync();

        await fixture.DeviceRepository.DidNotReceiveWithAnyArgs().UpdateLastAlertedOutcomeAsync(default, default, default);
        Assert.IsNull(fixture.SingleAttempt().SentryEventID);
    }

    [TestMethod]
    [DataRow(ProviderOutcome.Transport, 2)]
    [DataRow(ProviderOutcome.ProviderServerError, 2)]
    [DataRow(ProviderOutcome.Throttled, 1)]
    [DataRow(ProviderOutcome.AuthFailed, 1)]
    [DataRow(ProviderOutcome.ValidationFailed, 1)]
    [DataRow(ProviderOutcome.DeviceFaultOrOffline, 1)]
    [DataRow(ProviderOutcome.ParseFailure, 1)]
    public async Task RetryPolicy_RetriesOnlyTransportAndServerFailures(ProviderOutcome firstOutcome, int expectedCalls)
    {
        RunnerFixture fixture = new RunnerFixture("1");
        fixture.TelemetryResults.Enqueue(Result(firstOutcome));
        fixture.TelemetryResults.Enqueue(Result(ProviderOutcome.Success, new ProviderReading("SoC", "%", 60m, null)));

        await fixture.RunAsync();

        await fixture.Provider.Received(expectedCalls).GetLatestTelemetryAsync("DEVICE-1", Arg.Any<CancellationToken>());
    }

    [TestMethod]
    public async Task OneDeviceThrowing_DoesNotPreventNextDeviceAttempt()
    {
        RunnerFixture fixture = new RunnerFixture("1", deviceCount: 2);
        fixture.Provider.GetLatestTelemetryAsync("DEVICE-1", Arg.Any<CancellationToken>())
            .Returns<Task<ProviderCallResult>>(_ => throw new InvalidOperationException("fixture"));
        fixture.Provider.GetLatestTelemetryAsync("DEVICE-2", Arg.Any<CancellationToken>())
            .Returns(Result(ProviderOutcome.Success, new ProviderReading("SoC", "%", 60m, null)));

        await fixture.RunAsync();

        await fixture.Provider.Received(1).GetLatestTelemetryAsync("DEVICE-1", Arg.Any<CancellationToken>());
        await fixture.Provider.Received(1).GetLatestTelemetryAsync("DEVICE-2", Arg.Any<CancellationToken>());
        await fixture.RunRepository.Received(1).CompleteAsync(
            100,
            Arg.Any<DateTime>(),
            2,
            1,
            1,
            "PartialFailure",
            Arg.Any<CollectionRunStatusEvidence>(),
            Arg.Any<CancellationToken>());
    }

    private static DeviceCapability Capability(string sourceVariable, bool isExpected, DateTime? retiredAtUtc = null)
    {
        return new DeviceCapability(42, sourceVariable, "kW", isExpected, null, retiredAtUtc, DateTime.UtcNow, DateTime.UtcNow);
    }

    private static ProviderCallResult Result(ProviderOutcome outcome, params ProviderReading[] readings)
    {
        ProviderTelemetrySnapshot? snapshot = readings.Length == 0
            ? null
            : new ProviderTelemetrySnapshot(
                "DEVICE-1",
                DateTimeOffset.UtcNow,
                "2026-09-20 12:00:00 +00:00",
                new DateTimeOffset(2026, 9, 20, 12, 0, 0, TimeSpan.Zero),
                readings);
        return new ProviderCallResult(
            outcome,
            null,
            200,
            0,
            "fixture",
            readings.Select(item => item.Variable).ToArray(),
            null,
            null,
            snapshot);
    }

    private static void AssertObservation(
        IReadOnlyList<TelemetryObservationRecord> observations,
        string sourceVariable,
        string quantity,
        string quality,
        decimal? value)
    {
        TelemetryObservationRecord observation = observations.Single(item => item.SourceVariable == sourceVariable);
        Assert.AreEqual(quantity, observation.Quantity);
        Assert.AreEqual(quality, observation.Quality);
        Assert.AreEqual(value, observation.ValueParsed);
    }

    private sealed class RunnerFixture
    {
        private readonly List<(CollectionAttemptRecord Attempt, IReadOnlyList<TelemetryObservationRecord> Observations)> _captures = new();

        public RunnerFixture(string status, int deviceCount = 1)
        {
            Provider = Substitute.For<ITelemetryProvider>();
            SiteRepository = Substitute.For<ISiteRepository>();
            DeviceRepository = Substitute.For<IDeviceRepository>();
            CapabilityRepository = Substitute.For<IDeviceCapabilityRepository>();
            RunRepository = Substitute.For<ICollectionRunRepository>();
            CaptureRepository = Substitute.For<ICaptureRepository>();
            EvidenceWriter = Substitute.For<IEvidenceWriter>();

            Provider.ProviderKey.Returns("FoxESS");
            List<ProviderDevice> providerDevices = new List<ProviderDevice>();
            List<Device> devices = new List<Device>();
            for (int index = 1; index <= deviceCount; index++)
            {
                string providerDeviceId = $"DEVICE-{index}";
                providerDevices.Add(new ProviderDevice(providerDeviceId, "SITE-1", status, null, true, true, null));
                devices.Add(new Device
                {
                    ID = index == 1 ? 42 : 43,
                    SiteID = 5,
                    ProviderDeviceID = providerDeviceId,
                    Status = status,
                    HasPV = true,
                    HasBattery = true
                });
            }

            Discovery = new ProviderDiscoveryResult(ProviderOutcome.Success, 200, 0, "ok", null, null, providerDevices);
            Provider.DiscoverDevicesAsync("SITE-1", Arg.Any<CancellationToken>())
                .Returns(_ => Discovery);

            Site site = new Site { ID = 5, ProviderKey = "FoxESS", ProviderSiteID = "SITE-1", Name = "SITE-1" };
            SiteRepository.GetByProviderIdAsync("FoxESS", "SITE-1", Arg.Any<CancellationToken>()).Returns(site);
            SiteRepository.UpsertAsync("FoxESS", "SITE-1", "SITE-1", null, Arg.Any<CancellationToken>()).Returns(site);
            DeviceRepository.GetBySiteAsync(5, Arg.Any<CancellationToken>()).Returns(devices);
            DeviceRepository.UpsertAsync(
                Arg.Any<long>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string?>(),
                Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
                .Returns(call => devices.Single(item => item.ProviderDeviceID == call.ArgAt<string>(1)));
            CapabilityRepository.GetByDeviceAsync(Arg.Any<long>(), Arg.Any<CancellationToken>())
                .Returns(_ => Capabilities);
            RunRepository.StartAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>()).Returns(100L);
            CaptureRepository.RecordCaptureAsync(
                    Arg.Any<CollectionAttemptRecord>(),
                    Arg.Any<IReadOnlyList<TelemetryObservationRecord>>(),
                    Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    CollectionAttemptRecord attempt = call.ArgAt<CollectionAttemptRecord>(0);
                    IReadOnlyList<TelemetryObservationRecord> observations = call.ArgAt<IReadOnlyList<TelemetryObservationRecord>>(1).ToArray();
                    _captures.Add((attempt, observations));
                    return 700L + _captures.Count;
                });
            Provider.GetLatestTelemetryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(_ => TelemetryResults.Dequeue());

            CollectionOptions options = new CollectionOptions
            {
                ProviderSiteIDs = new[] { "SITE-1" },
                RetryDelay = TimeSpan.Zero
            };
            Runner = new CollectionRunner(
                Provider,
                SiteRepository,
                DeviceRepository,
                CapabilityRepository,
                RunRepository,
                CaptureRepository,
                EvidenceWriter,
                Options.Create(options));
        }

        public ITelemetryProvider Provider { get; }

        public ISiteRepository SiteRepository { get; }

        public IDeviceRepository DeviceRepository { get; }

        public IDeviceCapabilityRepository CapabilityRepository { get; }

        public IEvidenceWriter EvidenceWriter { get; }

        public ICollectionRunRepository RunRepository { get; }

        public ICaptureRepository CaptureRepository { get; }

        public CollectionRunner Runner { get; }

        public Queue<ProviderCallResult> TelemetryResults { get; } = new();

        public IReadOnlyList<DeviceCapability> Capabilities { get; set; } = Array.Empty<DeviceCapability>();

        public ProviderDiscoveryResult Discovery { get; set; }

        public Task RunAsync()
        {
            return Runner.RunOnceAsync(CancellationToken.None);
        }

        public CollectionAttemptRecord SingleAttempt()
        {
            return _captures.Single().Attempt;
        }

        public IReadOnlyList<TelemetryObservationRecord> SingleCaptureObservations()
        {
            return _captures.Single().Observations;
        }
    }
}
