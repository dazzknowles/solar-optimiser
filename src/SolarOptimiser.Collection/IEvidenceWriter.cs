namespace SolarOptimiser.Collection
{
    /// <summary>
    /// Writes sanitized request/response evidence to disk (SOL-T-502). Best-effort and secondary: a write
    /// failure never blocks or rolls back the primary database write, it just leaves the path null.
    /// </summary>
    public interface IEvidenceWriter
    {
        /// <summary>
        /// Writes <paramref name="content"/> (already sanitized by the caller) under
        /// <c>&lt;RawResponseRoot&gt;/yyyy/MM/dd/&lt;evidenceId&gt;-&lt;suffix&gt;.json</c> via a temp-file-then-atomic-rename,
        /// returning the final path only once the rename succeeds. Returns null if no <c>RawResponseRoot</c> is
        /// configured, the content is null/empty, the content exceeds the configured size cap, or the write
        /// itself fails for any reason.
        /// </summary>
        Task<string?> WriteAsync(string evidenceId, string suffix, string? content, DateTime atUtc, CancellationToken cancellationToken);
    }
}
