namespace SolarOptimiser.Providers.Abstractions
{
    public enum ProviderOutcome
    {
        Success,
        PartialMissing,
        DeviceFaultOrOffline,
        Throttled,
        AuthFailed,
        ValidationFailed,
        ProviderServerError,
        Transport,
        ParseFailure
    }
}
