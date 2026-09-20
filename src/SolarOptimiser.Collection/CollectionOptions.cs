namespace SolarOptimiser.Collection
{
    /// <summary>
    /// SOL-T-1201's <c>CollectionOptions</c> configuration section.
    /// </summary>
    public sealed class CollectionOptions
    {
        /// <summary>The site identifiers (in the configured provider's own ID space) this process polls.</summary>
        public IReadOnlyList<string> ProviderSiteIDs { get; set; } = Array.Empty<string>();

        public TimeSpan PollInterval { get; set; } = TimeSpan.FromMinutes(5);

        public int RequestTimeoutSeconds { get; set; } = 30;

        /// <summary>Delay before the single retry SOL-T-802 allows for Transport/ProviderServerError outcomes.</summary>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);

        /// <summary>The per-poll wall-clock/call budget referenced by SOL-T-801/SOL-T-804.</summary>
        public int PerPollBudget { get; set; } = 100;

        /// <summary>Default matches SOL-T-502's "default 1MB" cap on a single captured evidence file.</summary>
        public int MaxCapturedEvidenceBytes { get; set; } = 1_048_576;
    }
}
