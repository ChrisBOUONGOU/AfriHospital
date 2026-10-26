using AfriHospital.src.Domain.Entities;

namespace AfriHospital.src.Domain.Interfaces;

public interface IConsultationRepository
{
    Task<IReadOnlyList<Consultation>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken = default);
    Task AddAsync(Consultation consultation, CancellationToken cancellationToken = default);
}
