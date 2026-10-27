using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace AfriHospital.src.Infrastructure.Persistence;

/// <summary>
/// Idempotently imports records from the legacy UI tables into the GUID-based domain tables.
/// Legacy tables are intentionally retained as the authoritative source until all UI writes are migrated.
/// </summary>
public static class LegacyDomainDataMigration
{
    public static async Task<int> ImportAsync(SqliteConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var imported = 0;

        var patientsExist = await TableExistsAsync(connection, transaction, "patients", cancellationToken);
        if (!patientsExist)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var legacyPatients = new List<LegacyPatient>();
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT id, name, phone, birth_date, gender, created_at FROM patients ORDER BY id;";
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                legacyPatients.Add(new LegacyPatient(
                    reader.GetInt64(0), reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                    reader.IsDBNull(5) ? "" : reader.GetString(5)));
        }

        foreach (var legacy in legacyPatients)
        {
            var parts = SplitName(legacy.Name);
            var birth = DateOnly.TryParseExact(legacy.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            var created = DateTime.TryParse(legacy.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var createdAt)
                ? createdAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : DateTime.UnixEpoch.ToString("O", CultureInfo.InvariantCulture);

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT OR IGNORE INTO domain_patients
                    (id, patient_number, first_name, last_name, date_of_birth, sex, phone_number, created_at_utc, legacy_patient_id)
                VALUES ($id, $number, $first, $last, $birth, $sex, $phone, $created, $legacyId);
                """;
            insert.Parameters.AddWithValue("$id", StableGuid($"legacy-patient:{legacy.Id}").ToString("D"));
            insert.Parameters.AddWithValue("$number", $"LEGACY-{legacy.Id}");
            insert.Parameters.AddWithValue("$first", parts.First);
            insert.Parameters.AddWithValue("$last", parts.Last);
            insert.Parameters.AddWithValue("$birth", (object?)birth ?? DBNull.Value);
            insert.Parameters.AddWithValue("$sex", string.IsNullOrWhiteSpace(legacy.Gender) ? DBNull.Value : legacy.Gender);
            insert.Parameters.AddWithValue("$phone", string.IsNullOrWhiteSpace(legacy.Phone) ? DBNull.Value : legacy.Phone);
            insert.Parameters.AddWithValue("$created", created);
            insert.Parameters.AddWithValue("$legacyId", legacy.Id);
            imported += await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        if (await TableExistsAsync(connection, transaction, "consultations", cancellationToken))
        {
            var legacyConsultations = new List<LegacyConsultation>();
            await using (var read = connection.CreateCommand())
            {
                read.Transaction = (SqliteTransaction)transaction;
                read.CommandText = """
                    SELECT id, patient_id, patient_name, consulted_at, clinician, summary
                    FROM consultations ORDER BY id;
                    """;
                await using var reader = await read.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    legacyConsultations.Add(new LegacyConsultation(
                        reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetInt64(1),
                        reader.IsDBNull(2) ? "" : reader.GetString(2),
                        reader.IsDBNull(3) ? "" : reader.GetString(3),
                        reader.IsDBNull(4) ? "" : reader.GetString(4),
                        reader.IsDBNull(5) ? "" : reader.GetString(5)));
            }

            foreach (var legacy in legacyConsultations)
            {
                if (legacy.PatientId is null ||
                    !DateTime.TryParse(legacy.ConsultedAt, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal, out var consultationDate))
                    continue;

                await using var insert = connection.CreateCommand();
                insert.Transaction = (SqliteTransaction)transaction;
                insert.CommandText = """
                    INSERT OR IGNORE INTO domain_consultations
                        (id, patient_id, practitioner_id, consultation_date_utc, clinical_notes, diagnosis, legacy_consultation_id)
                    SELECT $id, p.id, $practitioner, $date, $notes, NULL, $legacyId
                    FROM domain_patients p WHERE p.legacy_patient_id=$patientId;
                    """;
                insert.Parameters.AddWithValue("$id", StableGuid($"legacy-consultation:{legacy.Id}").ToString("D"));
                insert.Parameters.AddWithValue("$practitioner", StableGuid($"legacy-practitioner:{(string.IsNullOrWhiteSpace(legacy.Clinician) ? "unknown" : legacy.Clinician.Trim().ToUpperInvariant())}").ToString("D"));
                insert.Parameters.AddWithValue("$date", consultationDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                insert.Parameters.AddWithValue("$notes", (object?)legacy.Summary ?? DBNull.Value);
                insert.Parameters.AddWithValue("$legacyId", legacy.Id);
                insert.Parameters.AddWithValue("$patientId", legacy.PatientId.Value);
                imported += await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return imported;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, SqliteTransaction transaction, string table, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name;";
        command.Parameters.AddWithValue("$name", table);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static (string First, string Last) SplitName(string fullName)
    {
        var normalized = fullName.Trim();
        var split = normalized.IndexOf(' ');
        return split < 0 ? (normalized, "(nom non séparé)") :
            (normalized[..split].Trim(), normalized[(split + 1)..].Trim());
    }

    private static Guid StableGuid(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }

    private sealed record LegacyPatient(long Id, string Name, string Phone, string BirthDate, string Gender, string CreatedAt);
    private sealed record LegacyConsultation(long Id, long? PatientId, string PatientName, string ConsultedAt, string Clinician, string Summary);
}
