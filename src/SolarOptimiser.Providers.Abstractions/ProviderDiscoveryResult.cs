namespace SolarOptimiser.Providers.Abstractions
{
    public sealed record ProviderDiscoveryResult(
        ProviderOutcome Outcome,
        int? HTTPStatus,
        int? ProviderErrorNumber,
        string? ProviderMessage,
        string? RequestSanitized,
        string? RawResponseSanitized,
        IReadOnlyList<ProviderDevice>? Devices); // null unless Outcome allows it
}
