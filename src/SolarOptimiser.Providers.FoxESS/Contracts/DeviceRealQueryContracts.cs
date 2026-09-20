using System.Text.Json.Serialization;

namespace SolarOptimiser.Providers.FoxESS.Contracts
{
    /// <summary>
    /// <c>POST /op/v1/device/real/query</c> request body (r04 §4.3). <c>Variables</c> is deliberately omitted
    /// from serialization when null — omitting it entirely requests every variable the device currently exposes
    /// (SOL-T-203).
    /// </summary>
    public sealed class FoxESSDeviceRealQueryRequest
    {
        [JsonPropertyName("sns")]
        public List<string> Sns { get; set; } = new List<string>();

        [JsonPropertyName("variables")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Variables { get; set; }
    }

    public sealed class FoxESSDeviceRealQueryResponse
    {
        [JsonPropertyName("errno")]
        public int Errno { get; set; }

        [JsonPropertyName("msg")]
        public string? Msg { get; set; }

        [JsonPropertyName("result")]
        public List<FoxESSDeviceRealQueryDeviceResult>? Result { get; set; }
    }

    public sealed class FoxESSDeviceRealQueryDeviceResult
    {
        [JsonPropertyName("deviceSN")]
        public string DeviceSN { get; set; } = string.Empty;

        [JsonPropertyName("time")]
        public string? Time { get; set; }

        [JsonPropertyName("datas")]
        public List<FoxESSDeviceRealQueryDatum> Datas { get; set; } = new List<FoxESSDeviceRealQueryDatum>();
    }

    public sealed class FoxESSDeviceRealQueryDatum
    {
        [JsonPropertyName("variable")]
        public string Variable { get; set; } = string.Empty;

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("value")]
        public decimal? Value { get; set; }
    }
}
