namespace AfriHospital.src.Domain.Entities;

public sealed class Appointment
{
    public long Id { get; set; }
    public Guid? PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Planifié";
}
