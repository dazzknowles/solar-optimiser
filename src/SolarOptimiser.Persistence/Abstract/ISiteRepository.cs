using SolarOptimiser.Domain;

namespace SolarOptimiser.Persistence.Abstract
{
    public interface ISiteRepository
    {
        Task<Site?> GetByProviderIdAsync(string providerKey, string providerSiteId, CancellationToken cancellationToken);

        Task<Site> UpsertAsync(string providerKey, string providerSiteId, string name, string? timeZone, CancellationToken cancellationToken);
    }
}
