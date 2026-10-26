using AfriHospital.src.Domain.Entities;

namespace AfriHospital.src.Domain.Interfaces;

public interface IHospitalUserRepository
{
    Task<int> CountAsync(CancellationToken cancellationToken = default);
    Task CreateInitialAdministratorAsync(HospitalUser user, CancellationToken cancellationToken = default);
    Task<HospitalUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(HospitalUser user, CancellationToken cancellationToken = default);
    Task UpdateLastKnownStateAsync(HospitalUser user, CancellationToken cancellationToken = default);
}
