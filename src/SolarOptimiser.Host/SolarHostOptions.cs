namespace SolarOptimiser.Host
{
    /// <summary>
    /// SOL-T-1201/SOL-T-1101: the retrieval API's LAN trust boundary and evidence storage root.
    /// </summary>
    public sealed class SolarHostOptions
    {
        public IReadOnlyList<string> AllowedCIDRRanges { get; set; } = Array.Empty<string>();

        public string RawResponseRoot { get; set; } = string.Empty;
    }
}
