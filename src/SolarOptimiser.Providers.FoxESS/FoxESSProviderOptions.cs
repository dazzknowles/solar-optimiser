namespace SolarOptimiser.Providers.FoxESS
{
    /// <summary>
    /// SOL-T-1201. <see cref="BaseUrl"/> defaults to the documented OpenAPI request domain (r04 §2), confirmed
    /// against tenant zero on 2026-09-21 — <c>developer-eu.foxesscloud.com</c> (the previous default) is the
    /// developer portal's web UI, not the API host, and rejects real API calls with a 405.
    /// </summary>
    public sealed class FoxESSProviderOptions
    {
        public string BaseUrl { get; set; } = "https://www.foxesscloud.com";

        public string ApiKey { get; set; } = string.Empty;

        public List<string> SiteProviderIDs { get; set; } = new List<string>();
    }
}
