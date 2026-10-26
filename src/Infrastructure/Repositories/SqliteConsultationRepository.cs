using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Domain.Interfaces;
using AfriHospital.src.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace AfriHospital.src.Infrastructure.Repositories;

public sealed class SqliteConsultationRepository(SqliteConnectionFactory factory) : IConsultationRepository
{
    public async Task<IReadOnlyList<Consultation>> GetForPatientAsync(Guid patientId, CancellationToken cancellationToken = default)
    {
        var items = new List<Consultation>();
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, patient_id, practitioner_id, consultation_date_utc, clinical_notes, diagnosis
            FROM domain_consultations WHERE patient_id=$patientId
            ORDER BY consultation_date_utc DESC LIMIT 1000;
            """;
        command.Parameters.AddWithValue("$patientId", patientId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            items.Add(new Consultation
            {
                Id = Guid.Parse(reader.GetString(0)), PatientId = Guid.Parse(reader.GetString(1)),
                PractitionerId = Guid.Parse(reader.GetString(2)),
                ConsultationDateUtc = DateTime.Parse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                ClinicalNotes = reader.IsDBNull(4) ? null : reader.GetString(4),
                Diagnosis = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        return items;
    }

    public async Task AddAsync(Consultation consultation, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consultation);
        if (consultation.Id == Guid.Empty) consultation.Id = Guid.NewGuid();
        if (consultation.PatientId == Guid.Empty || consultation.PractitionerId == Guid.Empty)
            throw new ArgumentException("Le patient et le professionnel doivent être identifiés.");
        if (consultation.ConsultationDateUtc == default)
            throw new ArgumentException("La date de consultation est obligatoire.");

        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO domain_consultations
                (id, patient_id, practitioner_id, consultation_date_utc, clinical_notes, diagnosis)
            VALUES ($id, $patient, $practitioner, $date, $notes, $diagnosis);
            """;
        command.Parameters.AddWithValue("$id", consultation.Id.ToString("D"));
        command.Parameters.AddWithValue("$patient", consultation.PatientId.ToString("D"));
        command.Parameters.AddWithValue("$practitioner", consultation.PractitionerId.ToString("D"));
        command.Parameters.AddWithValue("$date", consultation.ConsultationDateUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$notes", (object?)consultation.ClinicalNotes ?? DBNull.Value);
        command.Parameters.AddWithValue("$diagnosis", (object?)consultation.Diagnosis ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
