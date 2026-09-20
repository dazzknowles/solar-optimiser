namespace SolarOptimiser.Providers.Abstractions
{
    public sealed record ProviderCallResult(
        ProviderOutcome Outcome,
        string? DeviceStatus,
        int? HTTPStatus,
        int? ProviderErrorNumber,
        string? ProviderMessage,
        IReadOnlyList<string>? ReturnedVariables,
        string? RequestSanitized,
        string? RawResponseSanitized,
        ProviderTelemetrySnapshot? Snapshot); // present whenever any readings came back, regardless of Outcome
}
