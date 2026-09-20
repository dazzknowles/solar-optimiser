namespace SolarOptimiser.Providers.FoxESS
{
    /// <summary>
    /// SOL-T-1201. <see cref="BaseUrl"/> defaults to the EU OpenPlatform host confirmed for tenant zero
    /// (SOL-T-202) — override only for a different region.
    /// </summary>
    public sealed class FoxESSProviderOptions
    {
        public string BaseUrl { get; set; } = "https://developer-eu.foxesscloud.com";

        public string ApiKey { get; set; } = string.Empty;

        public List<string> SiteProviderIDs { get; set; } = new List<string>();
    }
}
