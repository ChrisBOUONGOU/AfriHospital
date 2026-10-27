using AfriHospital.src.Domain.Entities;

namespace AfriHospital.src.Domain.Validation;

public static class ConsultationValidator
{
    public const int MaxPractitionerNameLength = 150;
    public const int MaxClinicalNotesLength = 4000;
    public const int MaxDiagnosisLength = 1000;

    public static void Validate(Consultation consultation)
    {
        ArgumentNullException.ThrowIfNull(consultation);
        if (consultation.PatientId == Guid.Empty || consultation.PractitionerId == Guid.Empty)
            throw new ArgumentException("Le patient et le professionnel doivent être identifiés.");
        if (consultation.ConsultationDateUtc == default)
            throw new ArgumentException("La date de consultation est obligatoire.");
        if (consultation.ConsultationDateUtc.Kind == DateTimeKind.Utc &&
            consultation.ConsultationDateUtc > DateTime.UtcNow.AddMinutes(5))
            throw new ArgumentException("La date de consultation ne peut pas être dans le futur.");
        if (string.IsNullOrWhiteSpace(consultation.PractitionerName) ||
            consultation.PractitionerName.Trim().Length > MaxPractitionerNameLength)
            throw new ArgumentException($"Le nom du professionnel est obligatoire et ne doit pas dépasser {MaxPractitionerNameLength} caractères.");
        if (string.IsNullOrWhiteSpace(consultation.ClinicalNotes) && string.IsNullOrWhiteSpace(consultation.Diagnosis))
            throw new ArgumentException("Le résumé ou le diagnostic de la consultation est obligatoire.");
        if (consultation.ClinicalNotes?.Trim().Length > MaxClinicalNotesLength)
            throw new ArgumentException($"Le résumé ne doit pas dépasser {MaxClinicalNotesLength} caractères.");
        if (consultation.Diagnosis?.Trim().Length > MaxDiagnosisLength)
            throw new ArgumentException($"Le diagnostic ne doit pas dépasser {MaxDiagnosisLength} caractères.");

        consultation.PractitionerName = consultation.PractitionerName.Trim();
        consultation.ClinicalNotes = NormalizeOptional(consultation.ClinicalNotes);
        consultation.Diagnosis = NormalizeOptional(consultation.Diagnosis);
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
