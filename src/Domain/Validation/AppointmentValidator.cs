using AfriHospital.src.Domain.Entities;

namespace AfriHospital.src.Domain.Validation;

public static class AppointmentValidator
{
    public const int MaxReasonLength = 250;
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        "Planifié", "Confirmé", "Arrivé", "Terminé", "Annulé"
    };

    public static void Validate(Appointment appointment, bool requireId = false)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        if (requireId && appointment.Id <= 0)
            throw new ArgumentException("L'identifiant du rendez-vous est invalide.");
        if (appointment.PatientId is null || appointment.PatientId == Guid.Empty)
            throw new ArgumentException("Un patient doit être sélectionné.");
        if (appointment.ScheduledAt == default)
            throw new ArgumentException("La date du rendez-vous est obligatoire.");
        if ((appointment.Reason?.Trim().Length ?? 0) > MaxReasonLength)
            throw new ArgumentException($"Le motif ne doit pas dépasser {MaxReasonLength} caractères.");
        if (string.IsNullOrWhiteSpace(appointment.Status))
            appointment.Status = "Planifié";
        if (!AllowedStatuses.Contains(appointment.Status))
            throw new ArgumentException("Le statut du rendez-vous n'est pas reconnu.");
        appointment.Reason = appointment.Reason?.Trim() ?? string.Empty;
    }
}
