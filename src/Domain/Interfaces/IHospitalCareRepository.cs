using AfriHospital.src.Domain.Entities;

namespace AfriHospital.src.Domain.Interfaces;

public interface IHospitalCareRepository
{
    Task<Guid> AdmitAsync(Guid patientId, Guid bedId, DateTime admittedAtUtc, string service, string reason, CancellationToken cancellationToken = default);
    Task DischargeAsync(Guid admissionId, DateTime dischargedAtUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HospitalAdmission>> GetAdmissionsAsync(bool includeDischarged = true, CancellationToken cancellationToken = default);
    Task<Guid> AddBedAsync(HospitalBed bed, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HospitalBed>> GetAvailableBedsAsync(CancellationToken cancellationToken = default);
    Task<Guid> RequestLabTestAsync(Guid patientId, Guid? consultationId, string testName, string requestedBy, CancellationToken cancellationToken = default);
    Task RecordLabResultAsync(Guid orderId, string result, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LaboratoryOrder>> GetLabOrdersAsync(Guid? patientId = null, CancellationToken cancellationToken = default);
}
