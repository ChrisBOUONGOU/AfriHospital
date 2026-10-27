using AfriHospital.src.Domain.Entities;

namespace AfriHospital.src.Domain.Validation;

public static class PatientValidator
{
    public const int MaxFullNameLength = 150;
    public const int MaxPhoneLength = 30;
    public const int MaxSexLength = 30;
    public const int MaxNotesLength = 2000;
    public const int MaxPatientNumberLength = 40;

    public static void Validate(Patient patient)
    {
        ArgumentNullException.ThrowIfNull(patient);
        patient.PatientNumber = patient.PatientNumber?.Trim() ?? string.Empty;
        patient.FirstName = patient.FirstName?.Trim() ?? string.Empty;
        patient.LastName = patient.LastName?.Trim() ?? string.Empty;
        patient.PhoneNumber = NormalizeOptional(patient.PhoneNumber);
        patient.Sex = NormalizeOptional(patient.Sex);
        patient.Notes = NormalizeOptional(patient.Notes);

        if (patient.PatientNumber.Length == 0 || patient.PatientNumber.Length > MaxPatientNumberLength)
            throw new ArgumentException($"Le numéro de patient est obligatoire et ne doit pas dépasser {MaxPatientNumberLength} caractères.");
        if (patient.FirstName.Length == 0 || patient.FullName.Length > MaxFullNameLength)
            throw new ArgumentException($"Le nom du patient est obligatoire et ne doit pas dépasser {MaxFullNameLength} caractères.");
        if (patient.PhoneNumber?.Length > MaxPhoneLength)
            throw new ArgumentException($"Le téléphone ne doit pas dépasser {MaxPhoneLength} caractères.");
        if (patient.Sex?.Length > MaxSexLength)
            throw new ArgumentException($"Le champ sexe ne doit pas dépasser {MaxSexLength} caractères.");
        if (patient.Notes?.Length > MaxNotesLength)
            throw new ArgumentException($"Les notes administratives ne doivent pas dépasser {MaxNotesLength} caractères.");
        if (patient.DateOfBirth is DateOnly birthDate && birthDate > DateOnly.FromDateTime(DateTime.Today))
            throw new ArgumentException("La date de naissance ne peut pas être dans le futur.");
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
