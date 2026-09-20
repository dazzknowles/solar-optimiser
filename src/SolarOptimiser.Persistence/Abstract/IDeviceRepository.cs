using SolarOptimiser.Domain;

namespace SolarOptimiser.Persistence.Abstract
{
    public interface IDeviceRepository
    {
        Task<IReadOnlyList<Device>> GetBySiteAsync(long siteId, CancellationToken cancellationToken);

        Task<Device?> GetByIdAsync(long deviceId, CancellationToken cancellationToken);

        Task<Device> UpsertAsync(
            long siteId,
            string providerDeviceId,
            string? moduleSerial,
            string status,
            string? model,
            bool hasPV,
            bool hasBattery,
            CancellationToken cancellationToken);

        Task UpdateLastAlertedOutcomeAsync(long deviceId, string? outcome, CancellationToken cancellationToken);
    }
}
