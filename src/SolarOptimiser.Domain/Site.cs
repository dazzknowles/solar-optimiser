namespace SolarOptimiser.Domain
{
    /// <summary>
    /// A physical site being monitored. Provider-free (SOL-T-301): identity carries the provider's own site
    /// identifier exactly as reported, but this type has no knowledge of how a provider adapter works.
    /// </summary>
    public sealed class Site
    {
        public long ID { get; set; }

        public string ProviderKey { get; set; } = string.Empty;

        public string ProviderSiteID { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? TimeZone { get; set; }

        public DateTime CreatedAtUTC { get; set; }

        public DateTime UpdatedAtUTC { get; set; }
    }
}
