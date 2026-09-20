namespace SolarOptimiser.Providers.Abstractions
{
    public sealed record ProviderReading(string Variable, string? Unit, decimal? Value, string? Channel);
}
