namespace SolarOptimiser.Collection
{
    /// <summary>
    /// The single entry point <c>SolarOptimiser.Host</c>'s polling <c>BackgroundService</c> (SOL-T-801) calls on
    /// every tick. Owns one full poll: discover devices, fetch telemetry per device, classify quality, persist,
    /// and record the run/attempt evidence (SOL-T-501). The timer/scheduling loop itself belongs to Host, not
    /// here — this type does one run and returns.
    /// </summary>
    public interface ICollectionRunner
    {
        Task RunOnceAsync(CancellationToken cancellationToken);
    }
}
