namespace SolarOptimiser.Persistence.Records
{
    /// <summary>
    /// The write shape for one <c>CollectionAttempts</c> row (SOL-T-401). <see cref="Outcome"/> and
    /// <see cref="DeviceStatus"/> are stored as their string names (the enum values themselves for
    /// <see cref="Outcome"/> — <c>Success | PartialMissing | DeviceFaultOrOffline | Throttled | AuthFailed |
    /// ValidationFailed | ProviderServerError | Transport | ParseFailure</c>).
    /// </summary>
    public sealed record CollectionAttemptRecord(
        long CollectionRunID,
        long DeviceID,
        DateTime RequestedAtUTC,
        DateTime? CompletedAtUTC,
        string Outcome,
        string? DeviceStatus,
        int? HTTPStatus,
        int? ProviderErrorNumber,
        string? ProviderMessage,
        string? RequestedVariables, // null = "all" (every poll omits FoxESS's variables parameter)
        string? ReturnedVariables,
        string? RequestPath,
        string? RawResponsePath,
        string? SentryEventID);
}
