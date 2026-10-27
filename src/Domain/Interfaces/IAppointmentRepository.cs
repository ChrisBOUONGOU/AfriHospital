using AfriHospital.src.Domain.Entities;

namespace AfriHospital.src.Domain.Interfaces;

public interface IAppointmentRepository
{
    Task<IReadOnlyList<Appointment>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> HasConflictAsync(Guid patientId, DateTime scheduledAt, long? excludingAppointmentId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default);
    Task CancelAsync(long appointmentId, CancellationToken cancellationToken = default);
}
