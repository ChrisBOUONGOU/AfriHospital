using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Domain.Interfaces;
using AfriHospital.src.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace AfriHospital.src.Infrastructure.Repositories;

/// <summary>
/// Domain-facing consultation repository backed by the consultations table used by the existing UI.
/// </summary>
public sealed class SqliteConsultationRepository(SqliteConnectionFactory factory) : IConsultationRepository
{
    public async Task<IReadOnlyList<Consultation>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await factory.InitializeAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);

        var items = new List<Consultation>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT dc.id, dc.patient_id, dc.practitioner_id, dc.consultation_date_utc, dc.clinical_notes, dc.diagnosis, COALESCE(lc.clinician, '')
            FROM domain_consultations dc LEFT JOIN consultations lc ON lc.id=dc.legacy_consultation_id
            ORDER BY dc.consultation_date_utc DESC LIMIT 1000;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(new Consultation
            {
                Id = Guid.Parse(reader.GetString(0)),
                PatientId = Guid.Parse(reader.GetString(1)),
                PractitionerId = Guid.Parse(reader.GetString(2)),
                ConsultationDateUtc = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                ClinicalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
                Diagnosis = reader.IsDBNull(5) ? null : reader.GetString(5),
                PractitionerName = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        return items;
    }

    public async Task<IReadOnlyList<Consultation>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        await factory.InitializeAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);

        var items = new List<Consultation>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT dc.id, dc.patient_id, dc.practitioner_id, dc.consultation_date_utc, dc.clinical_notes, dc.diagnosis, COALESCE(lc.clinician, '')
            FROM domain_consultations dc LEFT JOIN consultations lc ON lc.id=dc.legacy_consultation_id
            WHERE dc.patient_id=$patientId ORDER BY dc.consultation_date_utc DESC LIMIT 1000;
            """;
        command.Parameters.AddWithValue("$patientId", patientId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(new Consultation
            {
                Id = Guid.Parse(reader.GetString(0)),
                PatientId = Guid.Parse(reader.GetString(1)),
                PractitionerId = Guid.Parse(reader.GetString(2)),
                ConsultationDateUtc = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                ClinicalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
                Diagnosis = reader.IsDBNull(5) ? null : reader.GetString(5),
                PractitionerName = reader.IsDBNull(6) ? null : reader.GetString(6)
            });
        return items;
    }

    public async Task AddAsync(Consultation consultation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consultation);
        if (consultation.PatientId == Guid.Empty || consultation.PractitionerId == Guid.Empty)
            throw new ArgumentException("Le patient et le professionnel doivent être identifiés.");
        if (consultation.ConsultationDateUtc == default)
            throw new ArgumentException("La date de consultation est obligatoire.");

        await factory.InitializeAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);

        await using var practitioner = connection.CreateCommand();
        practitioner.CommandText = "SELECT username FROM hospital_users WHERE id=$id LIMIT 1;";
        practitioner.Parameters.AddWithValue("$id", consultation.PractitionerId.ToString("D"));
        var clinician = !string.IsNullOrWhiteSpace(consultation.PractitionerName)
            ? consultation.PractitionerName.Trim()
            : await practitioner.ExecuteScalarAsync(cancellationToken) as string
                ?? consultation.PractitionerId.ToString("D");

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO consultations(patient_id, patient_name, consulted_at, clinician, practitioner_id, summary, created_at)
            SELECT p.legacy_patient_id, legacy.name, $date, $clinician, $practitionerId, $summary, CURRENT_TIMESTAMP
            FROM domain_patients p
            JOIN patients legacy ON legacy.id=p.legacy_patient_id
            WHERE p.id=$patientId AND p.legacy_patient_id IS NOT NULL;
            """;
        command.Parameters.AddWithValue("$date", consultation.ConsultationDateUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$clinician", clinician);
        command.Parameters.AddWithValue("$practitionerId", consultation.PractitionerId.ToString("D"));
        command.Parameters.AddWithValue("$summary", CombineNotes(consultation.ClinicalNotes, consultation.Diagnosis));
        command.Parameters.AddWithValue("$patientId", consultation.PatientId.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new KeyNotFoundException("Le patient lié à cette consultation est introuvable.");
        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid();";
        var legacyId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT id FROM domain_consultations WHERE legacy_consultation_id=$legacyId;";
        lookup.Parameters.AddWithValue("$legacyId", legacyId);
        var id = await lookup.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("La consultation a été enregistrée, mais son identifiant métier n'a pas pu être créé.");
        consultation.Id = Guid.Parse(id);
    }

    private static string CombineNotes(string? notes, string? diagnosis) =>
        string.Join(Environment.NewLine, new[]
        {
            notes?.Trim() ?? "",
            string.IsNullOrWhiteSpace(diagnosis) ? "" : $"Diagnostic : {diagnosis.Trim()}"
        }.Where(s => !string.IsNullOrWhiteSpace(s)));
}
