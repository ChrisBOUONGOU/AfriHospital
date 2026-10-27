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

        imported += await ExportDomainOnlyPatientsAsync(connection, transaction, cancellationToken);
        imported += await ExportDomainOnlyConsultationsAsync(connection, transaction, cancellationToken);

        var legacyPatients = new List<LegacyPatient>();
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT id, name, patient_number, phone, birth_date, gender, created_at FROM patients ORDER BY id;";
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                legacyPatients.Add(new LegacyPatient(
                    reader.GetInt64(0), reader.GetString(1),
                    reader.IsDBNull(2) ? "" : reader.GetString(2),
                    reader.IsDBNull(3) ? "" : reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4),
                    reader.IsDBNull(5) ? "" : reader.GetString(5),
                    reader.IsDBNull(6) ? "" : reader.GetString(6)));
        }

        foreach (var legacy in legacyPatients)
        {
            var parts = SplitName(legacy.Name);
            var birth = DateOnly.TryParseExact(legacy.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : null;
            var created = DateTime.TryParse(legacy.CreatedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var createdAt)
                ? createdAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : DateTime.UnixEpoch.ToString("O", CultureInfo.InvariantCulture);

            var number = string.IsNullOrWhiteSpace(legacy.PatientNumber) ? $"LEGACY-{legacy.Id}" : legacy.PatientNumber.Trim();
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = """
                UPDATE domain_patients SET
                    patient_number=$number,
                    first_name=$first, last_name=$last, date_of_birth=$birth, sex=$sex,
                    phone_number=$phone, created_at_utc=$created
                WHERE legacy_patient_id=$legacyId;
                """;
            BindLegacyPatient(update, legacy, parts.First, parts.Last, birth, created, number);
            var updated = await update.ExecuteNonQueryAsync(cancellationToken);
            if (updated == 0)
            {
                await using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = """
                    INSERT OR IGNORE INTO domain_patients
                        (id, patient_number, first_name, last_name, date_of_birth, sex, phone_number, created_at_utc, legacy_patient_id)
                    VALUES ($id, $number, $first, $last, $birth, $sex, $phone, $created, $legacyId);
                    """;
                insert.Parameters.AddWithValue("$id", StableGuid($"legacy-patient:{legacy.Id}").ToString("D"));
                BindLegacyPatient(insert, legacy, parts.First, parts.Last, birth, created, number);
                imported += await insert.ExecuteNonQueryAsync(cancellationToken);
            }
            else imported += updated;
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

                var practitionerId = await ResolvePractitionerGuidAsync(connection, transaction, legacy.Clinician, cancellationToken);
                await using var update = connection.CreateCommand();
                update.Transaction = transaction;
                update.CommandText = """
                    UPDATE domain_consultations SET patient_id=(
                        SELECT id FROM domain_patients WHERE legacy_patient_id=$patientId
                    ), practitioner_id=$practitioner, consultation_date_utc=$date, clinical_notes=$notes
                    WHERE legacy_consultation_id=$legacyId;
                    """;
                update.Parameters.AddWithValue("$practitioner", practitionerId.ToString("D"));
                update.Parameters.AddWithValue("$date", consultationDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                update.Parameters.AddWithValue("$notes", legacy.Summary);
                update.Parameters.AddWithValue("$legacyId", legacy.Id);
                update.Parameters.AddWithValue("$patientId", legacy.PatientId.Value);
                var updated = await update.ExecuteNonQueryAsync(cancellationToken);
                if (updated == 0)
                {
                    await using var insert = connection.CreateCommand();
                    insert.Transaction = transaction;
                    insert.CommandText = """
                        INSERT OR IGNORE INTO domain_consultations
                            (id, patient_id, practitioner_id, consultation_date_utc, clinical_notes, diagnosis, legacy_consultation_id)
                        SELECT $id, p.id, $practitioner, $date, $notes, NULL, $legacyId
                        FROM domain_patients p WHERE p.legacy_patient_id=$patientId;
                        """;
                    insert.Parameters.AddWithValue("$id", StableGuid($"legacy-consultation:{legacy.Id}").ToString("D"));
                    insert.Parameters.AddWithValue("$practitioner", practitionerId.ToString("D"));
                    insert.Parameters.AddWithValue("$date", consultationDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                    insert.Parameters.AddWithValue("$notes", legacy.Summary);
                    insert.Parameters.AddWithValue("$legacyId", legacy.Id);
                    insert.Parameters.AddWithValue("$patientId", legacy.PatientId.Value);
                    imported += await insert.ExecuteNonQueryAsync(cancellationToken);
                }
                else imported += updated;
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return imported;
    }

    private static void BindLegacyPatient(SqliteCommand command, LegacyPatient legacy, string first, string last, string? birth, string created, string number)
    {
        command.Parameters.AddWithValue("$number", number);
        command.Parameters.AddWithValue("$first", first);
        command.Parameters.AddWithValue("$last", last);
        command.Parameters.AddWithValue("$birth", (object?)birth ?? DBNull.Value);
        command.Parameters.AddWithValue("$sex", string.IsNullOrWhiteSpace(legacy.Gender) ? DBNull.Value : legacy.Gender);
        command.Parameters.AddWithValue("$phone", string.IsNullOrWhiteSpace(legacy.Phone) ? DBNull.Value : legacy.Phone);
        command.Parameters.AddWithValue("$created", created);
        command.Parameters.AddWithValue("$legacyId", legacy.Id);
    }

    private static async Task<int> ExportDomainOnlyPatientsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var patients = new List<DomainPatient>();
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = """
                SELECT id, patient_number, first_name, last_name, date_of_birth, sex, phone_number, created_at_utc
                FROM domain_patients WHERE legacy_patient_id IS NULL ORDER BY rowid;
                """;
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                patients.Add(new DomainPatient(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.IsDBNull(4) ? "" : reader.GetString(4), reader.IsDBNull(5) ? "" : reader.GetString(5),
                    reader.IsDBNull(6) ? "" : reader.GetString(6), reader.GetString(7)));
        }

        foreach (var patient in patients)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO patients(name, patient_number, phone, birth_date, gender, notes, created_at)
                VALUES($name, $number, $phone, $birth, $gender, '', $created);
                """;
            insert.Parameters.AddWithValue("$name", $"{patient.FirstName} {patient.LastName}".Trim());
            insert.Parameters.AddWithValue("$number", patient.PatientNumber);
            insert.Parameters.AddWithValue("$phone", patient.Phone);
            insert.Parameters.AddWithValue("$birth", patient.BirthDate);
            insert.Parameters.AddWithValue("$gender", patient.Sex);
            insert.Parameters.AddWithValue("$created", patient.CreatedAt);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            await using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT last_insert_rowid();";
            var legacyId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE domain_patients SET legacy_patient_id=$legacyId WHERE id=$id;";
            update.Parameters.AddWithValue("$legacyId", legacyId);
            update.Parameters.AddWithValue("$id", patient.Id);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        return patients.Count;
    }

    private static async Task<int> ExportDomainOnlyConsultationsAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken cancellationToken)
    {
        var consultations = new List<DomainConsultation>();
        await using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = """
                SELECT c.id, c.patient_id, c.practitioner_id, c.consultation_date_utc, c.clinical_notes, c.diagnosis
                FROM domain_consultations c
                WHERE c.legacy_consultation_id IS NULL
                ORDER BY c.rowid;
                """;
            await using var reader = await read.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                consultations.Add(new DomainConsultation(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                    reader.GetString(3), reader.IsDBNull(4) ? "" : reader.GetString(4),
                    reader.IsDBNull(5) ? "" : reader.GetString(5)));
        }

        foreach (var consultation in consultations)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO consultations(patient_id, patient_name, consulted_at, clinician, summary, created_at)
                SELECT p.legacy_patient_id, legacy.name, $date, COALESCE(u.username, $practitioner),
                       $summary, CURRENT_TIMESTAMP
                FROM domain_patients p
                JOIN patients legacy ON legacy.id=p.legacy_patient_id
                LEFT JOIN hospital_users u ON u.id=$practitioner
                WHERE p.id=$patientId AND p.legacy_patient_id IS NOT NULL;
                """;
            insert.Parameters.AddWithValue("$date", consultation.Date);
            insert.Parameters.AddWithValue("$practitioner", consultation.PractitionerId);
            insert.Parameters.AddWithValue("$summary", CombineNotes(consultation.Notes, consultation.Diagnosis));
            insert.Parameters.AddWithValue("$patientId", consultation.PatientId);
            var rows = await insert.ExecuteNonQueryAsync(cancellationToken);
            if (rows == 0) continue;
            await using var idCommand = connection.CreateCommand();
            idCommand.Transaction = transaction;
            idCommand.CommandText = "SELECT last_insert_rowid();";
            var legacyId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
            await using var update = connection.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE domain_consultations SET legacy_consultation_id=$legacyId WHERE id=$id;";
            update.Parameters.AddWithValue("$legacyId", legacyId);
            update.Parameters.AddWithValue("$id", consultation.Id);
            await update.ExecuteNonQueryAsync(cancellationToken);
        }
        return consultations.Count;
    }

    private static string CombineNotes(string notes, string diagnosis) =>
        string.Join(Environment.NewLine, new[] { notes.Trim(), string.IsNullOrWhiteSpace(diagnosis) ? "" : $"Diagnostic : {diagnosis.Trim()}" }.Where(s => !string.IsNullOrWhiteSpace(s)));

    public static Guid StablePatientGuid(long legacyId) => StableGuid($"legacy-patient:{legacyId}");
    public static Guid StableConsultationGuid(long legacyId) => StableGuid($"legacy-consultation:{legacyId}");
    public static Guid StablePractitionerGuid(string clinician) =>
        StableGuid($"legacy-practitioner:{(string.IsNullOrWhiteSpace(clinician) ? "unknown" : clinician.Trim().ToUpperInvariant())}");

    private static async Task<Guid> ResolvePractitionerGuidAsync(SqliteConnection connection, SqliteTransaction transaction, string clinician, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(clinician, out var directId)) return directId;
        if (!string.IsNullOrWhiteSpace(clinician))
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT id FROM hospital_users WHERE username=$username COLLATE NOCASE LIMIT 1;";
            command.Parameters.AddWithValue("$username", clinician.Trim());
            var value = await command.ExecuteScalarAsync(cancellationToken) as string;
            if (Guid.TryParse(value, out var userId)) return userId;
        }
        return StablePractitionerGuid(clinician);
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

    private sealed record DomainPatient(string Id, string PatientNumber, string FirstName, string LastName, string BirthDate, string Sex, string Phone, string CreatedAt);
    private sealed record DomainConsultation(string Id, string PatientId, string PractitionerId, string Date, string Notes, string Diagnosis);
    private sealed record LegacyPatient(long Id, string Name, string PatientNumber, string Phone, string BirthDate, string Gender, string CreatedAt);
    private sealed record LegacyConsultation(long Id, long? PatientId, string PatientName, string ConsultedAt, string Clinician, string Summary);
}
