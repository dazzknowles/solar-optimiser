using SolarOptimiser.Persistence.Records;

namespace SolarOptimiser.Persistence.Abstract
{
    /// <summary>
    /// SOL-T-302: every acquisition run's discovery/status call upserts a row for every variable seen, mapped or
    /// not. A first-ever sighting is unreviewed (<c>IsExpected = 0</c>); <see cref="ApproveAsync"/> is the one-way
    /// <c>0 -&gt; 1</c> transition; <see cref="RetireAsync"/> sets <c>RetiredAtUTC</c> as a separate, additive state.
    /// </summary>
    public interface IDeviceCapabilityRepository
    {
        Task UpsertSeenAsync(long deviceId, string sourceVariable, string? unit, DateTime seenAtUTC, CancellationToken cancellationToken);

        Task ApproveAsync(long deviceId, string sourceVariable, CancellationToken cancellationToken);

        Task RetireAsync(long deviceId, string sourceVariable, CancellationToken cancellationToken);

        Task<IReadOnlyList<DeviceCapability>> GetByDeviceAsync(long deviceId, CancellationToken cancellationToken);
    }
}
