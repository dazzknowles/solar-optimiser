using System.Text.Json.Serialization;

namespace SolarOptimiser.Providers.FoxESS.Contracts
{
    /// <summary>
    /// <c>POST /op/v0/device/list</c> raw response shape (r04 §4.2). FoxESS's captured documentation gives the
    /// field list in prose, not a JSON schema, so the envelope (<c>errno</c>/<c>result</c>) and exact station
    /// field names here are a best-effort reconstruction pending tenant-zero verification.
    /// </summary>
    public sealed class FoxESSDeviceListResponse
    {
        [JsonPropertyName("errno")]
        public int Errno { get; set; }

        [JsonPropertyName("msg")]
        public string? Msg { get; set; }

        [JsonPropertyName("result")]
        public FoxESSDeviceListResult? Result { get; set; }
    }

    public sealed class FoxESSDeviceListResult
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("data")]
        public List<FoxESSDeviceListItem> Data { get; set; } = new List<FoxESSDeviceListItem>();
    }

    public sealed class FoxESSDeviceListItem
    {
        [JsonPropertyName("deviceSN")]
        public string DeviceSN { get; set; } = string.Empty;

        [JsonPropertyName("moduleSN")]
        public string? ModuleSN { get; set; }

        [JsonPropertyName("stationID")]
        public string? StationID { get; set; }

        [JsonPropertyName("stationName")]
        public string? StationName { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("hasPV")]
        public bool HasPV { get; set; }

        [JsonPropertyName("hasBattery")]
        public bool HasBattery { get; set; }

        [JsonPropertyName("deviceType")]
        public string? DeviceType { get; set; }
    }
}
