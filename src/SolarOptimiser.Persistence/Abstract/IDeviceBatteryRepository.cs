namespace SolarOptimiser.Persistence.Abstract
{
    public interface IDeviceBatteryRepository
    {
        Task UpsertAsync(
            long deviceId,
            string batterySerial,
            string? batteryType,
            string? model,
            string? capacityRaw,
            string? manufacturedAtRaw,
            CancellationToken cancellationToken);
    }
}
