using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Domain.Interfaces;
using AfriHospital.src.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace AfriHospital.src.Infrastructure.Repositories;

/// <summary>
/// Domain-facing repository backed by the same patients table used by the existing Avalonia UI.
/// The GUID table is a compatibility projection maintained by LegacyDomainDataMigration.
/// </summary>
public sealed class SqlitePatientRepository(SqliteConnectionFactory factory) : IPatientRepository
{
    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, patient_number, first_name, last_name, date_of_birth, sex, phone_number, created_at_utc
            FROM domain_patients WHERE id=$id;
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPatient(reader) : null;
    }

    public async Task<IReadOnlyList<Patient>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        await EnsureReadyAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);
        var patients = new List<Patient>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, patient_number, first_name, last_name, date_of_birth, sex, phone_number, created_at_utc
            FROM domain_patients
            WHERE patient_number LIKE $query OR first_name LIKE $query OR last_name LIKE $query OR phone_number LIKE $query
            ORDER BY last_name, first_name LIMIT 500;
            """;
        command.Parameters.AddWithValue("$query", $"%{query.Trim()}%");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) patients.Add(ReadPatient(reader));
        return patients;
    }

    public async Task AddAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);
        Validate(patient);
        await EnsureReadyAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO patients(name, patient_number, phone, birth_date, gender, notes, created_at)
            VALUES($name, $number, $phone, $birth, $sex, '', $created);
            """;
        BindLegacyPatient(command, patient);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid();";
        var legacyId = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);
        await using var lookup = connection.CreateCommand();
        lookup.CommandText = "SELECT id FROM domain_patients WHERE legacy_patient_id=$legacyId;";
        lookup.Parameters.AddWithValue("$legacyId", legacyId);
        var id = await lookup.ExecuteScalarAsync(cancellationToken) as string;
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Le patient a été enregistré, mais son identifiant métier n'a pas pu être créé.");
        patient.Id = Guid.Parse(id);
    }

    public async Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);
        if (patient.Id == Guid.Empty) throw new ArgumentException("L'identifiant du patient est invalide.");
        Validate(patient);
        await EnsureReadyAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE patients SET name=$name, patient_number=$number, phone=$phone,
                birth_date=$birth, gender=$sex WHERE id=(
                    SELECT legacy_patient_id FROM domain_patients WHERE id=$id
                );
            """;
        BindLegacyPatient(command, patient);
        command.Parameters.AddWithValue("$id", patient.Id.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new KeyNotFoundException("Patient introuvable.");
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);
    }

    private async Task EnsureReadyAsync(CancellationToken cancellationToken) =>
        await factory.InitializeAsync(cancellationToken);

    private static void Validate(Patient patient)
    {
        if (string.IsNullOrWhiteSpace(patient.PatientNumber) ||
            string.IsNullOrWhiteSpace(patient.FirstName) || string.IsNullOrWhiteSpace(patient.LastName))
            throw new ArgumentException("Le numéro, le prénom et le nom du patient sont obligatoires.");
    }

    private static void BindLegacyPatient(SqliteCommand command, Patient patient)
    {
        command.Parameters.AddWithValue("$name", $"{patient.FirstName.Trim()} {patient.LastName.Trim()}".Trim());
        command.Parameters.AddWithValue("$number", patient.PatientNumber.Trim());
        command.Parameters.AddWithValue("$phone", (object?)patient.PhoneNumber ?? "");
        command.Parameters.AddWithValue("$birth", patient.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "");
        command.Parameters.AddWithValue("$sex", (object?)patient.Sex ?? "");
        command.Parameters.AddWithValue("$created", (patient.CreatedAtUtc == default ? DateTime.UtcNow : patient.CreatedAtUtc.ToUniversalTime())
            .ToString("O", CultureInfo.InvariantCulture));
    }

    private static Patient ReadPatient(SqliteDataReader reader) => new()
    {
        Id = Guid.Parse(reader.GetString(0)),
        PatientNumber = reader.GetString(1),
        FirstName = reader.GetString(2),
        LastName = reader.GetString(3),
        DateOfBirth = reader.IsDBNull(4) ? null : DateOnly.ParseExact(reader.GetString(4), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        Sex = reader.IsDBNull(5) ? null : reader.GetString(5),
        PhoneNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
        CreatedAtUtc = DateTime.Parse(reader.GetString(7), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
    };
}
