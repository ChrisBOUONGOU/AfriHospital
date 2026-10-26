using AfriHospital.src.Domain.Entities;

namespace AfriHospital.src.Domain.Interfaces;

public interface IHospitalUserRepository
{
    Task<HospitalUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task AddAsync(HospitalUser user, CancellationToken cancellationToken = default);
    Task UpdateLastKnownStateAsync(HospitalUser user, CancellationToken cancellationToken = default);
}
