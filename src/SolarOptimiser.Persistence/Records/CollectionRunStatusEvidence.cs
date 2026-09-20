namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// Evidence from the run's shared discovery/status call (SOL-T-501), recorded on <c>CollectionRuns.Status*</c>
    /// when the run is completed.
    /// </summary>
    public sealed record CollectionRunStatusEvidence(
        DateTime StatusCheckedAtUTC,
        int? StatusHTTPStatus,
        int? StatusProviderErrorNumber,
        string? StatusProviderMessage,
        string? StatusRequestPath,
        string? StatusResponsePath);
}
