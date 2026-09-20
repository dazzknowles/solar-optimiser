using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using SolarOptimiser.Providers.Abstractions;
using SolarOptimiser.Providers.FoxESS.Contracts;

namespace SolarOptimiser.Providers.FoxESS.Tests;

[TestClass]
public sealed class FoxESSProviderTests
{
    [TestMethod]
    public void Sign_KnownInputs_ReturnsExpectedLowercaseMd5()
    {
        FoxESSRequestSigner signer = new FoxESSRequestSigner();

        string signature = signer.Sign("/op/v1/device/real/query", "test-api-key", "1700000000123");

        Assert.AreEqual("b5fdf1721942e77e2f324e36e4f91143", signature);
    }

    [TestMethod]
    public async Task RealQuery_AddsAuthenticationHeadersAndOmitsVariables()
    {
        RecordingHandler handler = new RecordingHandler(HttpStatusCode.OK,
            "{\"errno\":0,\"msg\":\"ok\",\"result\":[]}");
        FoxESSHttpClient client = CreateClient(handler, "secret-token");

        await client.GetDeviceRealQueryAsync("DEVICE-1", CancellationToken.None);

        Assert.IsNotNull(handler.Request);
        Assert.AreEqual(HttpMethod.Post, handler.Request.Method);
        Assert.AreEqual("/op/v1/device/real/query", handler.Request.RequestUri!.AbsolutePath);
        Assert.AreEqual("secret-token", Header(handler.Request, "token"));
        Assert.AreEqual("en", Header(handler.Request, "lang"));
        Assert.IsTrue(long.TryParse(Header(handler.Request, "timestamp"), out long _));
        string expectedSignature = new FoxESSRequestSigner().Sign(
            "/op/v1/device/real/query",
            "secret-token",
            Header(handler.Request, "timestamp"));
        Assert.AreEqual(expectedSignature, Header(handler.Request, "signature"));
        Assert.AreEqual("{\"sns\":[\"DEVICE-1\"]}", handler.Body);
    }

    [TestMethod]
    public async Task DeviceList_FiltersRequestedSiteAndMapsDeviceFields()
    {
        string fixture = """
            {"errno":0,"msg":"ok","result":{"total":2,"data":[
              {"deviceSN":"A","moduleSN":"M1","stationID":"SITE-1","status":1,"hasPV":true,"hasBattery":true,"deviceType":"H3"},
              {"deviceSN":"B","moduleSN":"M2","stationID":"OTHER","status":3,"hasPV":true,"hasBattery":false,"deviceType":"S1"}
            ]}}
            """;
        FoxESSTelemetryProvider provider = CreateProvider(HttpStatusCode.OK, fixture);

        ProviderDiscoveryResult result = await provider.DiscoverDevicesAsync("SITE-1", CancellationToken.None);

        Assert.AreEqual(ProviderOutcome.Success, result.Outcome);
        Assert.IsNotNull(result.Devices);
        Assert.HasCount(1, result.Devices);
        ProviderDevice device = result.Devices[0];
        Assert.AreEqual("A", device.ProviderDeviceID);
        Assert.AreEqual("1", device.Status,
            "The trust-gate contract defines provider status value 1 as the trusted online state.");
        Assert.AreEqual("M1", device.ModuleSerial);
        Assert.IsTrue(device.HasPV);
        Assert.IsTrue(device.HasBattery);
        Assert.AreEqual("H3", device.Model);
    }

    [TestMethod]
    public async Task RealQuery_ParsesReadingsTimestampAndPvChannel()
    {
        string fixture = """
            {"errno":0,"msg":"ok","result":[{"deviceSN":"DEVICE-1","time":"2026-09-20 14:15:16 +01:00","datas":[
              {"variable":"pv3Power","unit":"kW","value":1.2345},
              {"variable":"meterPower2","unit":"W","value":-12.5}
            ]}]}
            """;
        FoxESSTelemetryProvider provider = CreateProvider(HttpStatusCode.OK, fixture);

        ProviderCallResult result = await provider.GetLatestTelemetryAsync("DEVICE-1", CancellationToken.None);

        Assert.AreEqual(ProviderOutcome.Success, result.Outcome);
        Assert.IsNotNull(result.Snapshot);
        Assert.AreEqual("2026-09-20 14:15:16 +01:00", result.Snapshot.ProviderTimestampRaw);
        Assert.AreEqual(new DateTimeOffset(2026, 9, 20, 14, 15, 16, TimeSpan.FromHours(1)), result.Snapshot.ProviderTimestampParsed);
        Assert.HasCount(2, result.Snapshot.Readings);
        Assert.AreEqual("3", result.Snapshot.Readings[0].Channel);
        Assert.IsNull(result.Snapshot.Readings[1].Channel);
        CollectionAssert.AreEqual(new[] { "pv3Power", "meterPower2" }, result.ReturnedVariables!.ToArray());
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Unauthorized, 0, ProviderOutcome.AuthFailed)]
    [DataRow(HttpStatusCode.Forbidden, 0, ProviderOutcome.AuthFailed)]
    [DataRow(HttpStatusCode.InternalServerError, 0, ProviderOutcome.ProviderServerError)]
    [DataRow(HttpStatusCode.OK, 40400, ProviderOutcome.Throttled)]
    [DataRow(HttpStatusCode.OK, 40256, ProviderOutcome.ValidationFailed)]
    [DataRow(HttpStatusCode.OK, 40257, ProviderOutcome.ValidationFailed)]
    [DataRow(HttpStatusCode.OK, 49999, ProviderOutcome.ProviderServerError)]
    public async Task RealQuery_ClassifiesHttpAndProviderErrors(
        HttpStatusCode statusCode,
        int errno,
        ProviderOutcome expected)
    {
        string fixture = $"{{\"errno\":{errno},\"msg\":\"fixture\",\"result\":[]}}";
        FoxESSTelemetryProvider provider = CreateProvider(statusCode, fixture);

        ProviderCallResult result = await provider.GetLatestTelemetryAsync("DEVICE-1", CancellationToken.None);

        Assert.AreEqual(expected, result.Outcome);
    }

    [TestMethod]
    public async Task RealQuery_MalformedJson_IsParseFailure()
    {
        FoxESSTelemetryProvider provider = CreateProvider(HttpStatusCode.OK, "{not-json");

        ProviderCallResult result = await provider.GetLatestTelemetryAsync("DEVICE-1", CancellationToken.None);

        Assert.AreEqual(ProviderOutcome.ParseFailure, result.Outcome);
        Assert.IsNull(result.Snapshot);
    }

    [TestMethod]
    public async Task RealQuery_EmptySuccessfulBody_IsParseFailureRatherThanSuccessfulEmptyCapture()
    {
        FoxESSTelemetryProvider provider = CreateProvider(HttpStatusCode.OK, string.Empty);

        ProviderCallResult result = await provider.GetLatestTelemetryAsync("DEVICE-1", CancellationToken.None);

        Assert.AreEqual(ProviderOutcome.ParseFailure, result.Outcome,
            "A 200 response with no parseable envelope must not be reported as Success.");
        Assert.IsNull(result.Snapshot);
    }

    [TestMethod]
    public async Task RealQuery_OneUnparseableValue_DoesNotDiscardOtherValidReadings()
    {
        string fixture = """
            {"errno":0,"msg":"ok","result":[{"deviceSN":"DEVICE-1","time":"2026-09-20 12:00:00 +00:00","datas":[
              {"variable":"SoC","unit":"%","value":70},
              {"variable":"pvPower","unit":"kW","value":"not-a-number"}
            ]}]}
            """;
        FoxESSTelemetryProvider provider = CreateProvider(HttpStatusCode.OK, fixture);

        ProviderCallResult result = await provider.GetLatestTelemetryAsync("DEVICE-1", CancellationToken.None);

        Assert.AreEqual(ProviderOutcome.Success, result.Outcome,
            "Per-variable invalid data must be additive and must not turn the whole response into a parse failure.");
        Assert.IsNotNull(result.Snapshot);
        Assert.HasCount(2, result.Snapshot.Readings);
        Assert.AreEqual(70m, result.Snapshot.Readings.Single(item => item.Variable == "SoC").Value);
        Assert.IsNull(result.Snapshot.Readings.Single(item => item.Variable == "pvPower").Value);
    }

    [TestMethod]
    public async Task DeviceList_SanitizedEvidence_RemovesAllowlistedLocationFields()
    {
        string fixture = """
            {"errno":0,"msg":"ok","address":"private home address","result":{"total":0,"data":[]}}
            """;
        RecordingHandler handler = new RecordingHandler(HttpStatusCode.OK, fixture);
        FoxESSHttpClient client = CreateClient(handler, "token");

        FoxESSApiCallResult<FoxESSDeviceListResponse> result = await client.GetDeviceListAsync(CancellationToken.None);

        Assert.DoesNotContain("private home address", result.RawResponseSanitized,
            "SOL-T-503 requires deterministic removal of plant address/user/installer fields from retained evidence.");
    }

    [TestMethod]
    public async Task RealQuery_TransportException_IsTransportFailure()
    {
        RecordingHandler handler = new RecordingHandler(new HttpRequestException("fixture"));
        FoxESSTelemetryProvider provider = new FoxESSTelemetryProvider(CreateClient(handler, "token"));

        ProviderCallResult result = await provider.GetLatestTelemetryAsync("DEVICE-1", CancellationToken.None);

        Assert.AreEqual(ProviderOutcome.Transport, result.Outcome);
        Assert.IsNull(result.HTTPStatus);
    }

    private static FoxESSTelemetryProvider CreateProvider(HttpStatusCode statusCode, string responseBody)
    {
        RecordingHandler handler = new RecordingHandler(statusCode, responseBody);
        return new FoxESSTelemetryProvider(CreateClient(handler, "token"));
    }

    private static FoxESSHttpClient CreateClient(HttpMessageHandler handler, string apiKey)
    {
        HttpClient httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://foxess.invalid")
        };
        FoxESSProviderOptions options = new FoxESSProviderOptions
        {
            BaseUrl = "https://foxess.invalid",
            ApiKey = apiKey
        };
        return new FoxESSHttpClient(httpClient, Options.Create(options), new FoxESSRequestSigner());
    }

    private static string Header(HttpRequestMessage request, string name)
    {
        return request.Headers.GetValues(name).Single();
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;
        private readonly Exception? _exception;

        public RecordingHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public RecordingHandler(Exception exception)
        {
            _exception = exception;
            _responseBody = string.Empty;
        }

        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            if (request.Content != null)
            {
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            }

            if (_exception != null)
            {
                throw _exception;
            }

            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
