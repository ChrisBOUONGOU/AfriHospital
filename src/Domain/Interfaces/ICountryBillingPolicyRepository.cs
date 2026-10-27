using AfriHospital.src.Domain.Entities;

namespace AfriHospital.src.Domain.Interfaces;

/// <summary>Stores explicitly configured country billing rules; it never assumes a statutory rate.</summary>
public interface ICountryBillingPolicyRepository
{
    Task SaveAsync(CountryBillingPolicy policy, CancellationToken cancellationToken = default);
    Task<CountryBillingPolicy?> GetByCountryAsync(string countryCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CountryBillingPolicy>> GetAllAsync(CancellationToken cancellationToken = default);
}
