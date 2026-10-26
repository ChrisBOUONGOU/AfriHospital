using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Domain.Interfaces;
using AfriHospital.src.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace AfriHospital.src.Infrastructure.Repositories;

public sealed class SqlitePatientRepository(SqliteConnectionFactory factory) : IPatientRepository
{
    public async Task<Patient?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, patient_number, first_name, last_name, date_of_birth, sex, phone_number, created_at_utc FROM domain_patients WHERE id=$id;";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPatient(reader) : null;
    }

    public async Task<IReadOnlyList<Patient>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var patients = new List<Patient>();
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
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
        if (patient.Id == Guid.Empty) patient.Id = Guid.NewGuid();
        Validate(patient);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO domain_patients
                (id, patient_number, first_name, last_name, date_of_birth, sex, phone_number, created_at_utc)
            VALUES ($id, $number, $first, $last, $birth, $sex, $phone, $created);
            """;
        BindPatient(command, patient);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(Patient patient, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(patient);
        if (patient.Id == Guid.Empty) throw new ArgumentException("L'identifiant du patient est invalide.");
        Validate(patient);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE domain_patients SET patient_number=$number, first_name=$first, last_name=$last,
                date_of_birth=$birth, sex=$sex, phone_number=$phone WHERE id=$id;
            """;
        BindPatient(command, patient);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new KeyNotFoundException("Patient introuvable.");
    }

    private static void Validate(Patient patient)
    {
        if (string.IsNullOrWhiteSpace(patient.PatientNumber) ||
            string.IsNullOrWhiteSpace(patient.FirstName) || string.IsNullOrWhiteSpace(patient.LastName))
            throw new ArgumentException("Le numéro, le prénom et le nom du patient sont obligatoires.");
    }

    private static void BindPatient(SqliteCommand command, Patient patient)
    {
        command.Parameters.AddWithValue("$id", patient.Id.ToString("D"));
        command.Parameters.AddWithValue("$number", patient.PatientNumber.Trim());
        command.Parameters.AddWithValue("$first", patient.FirstName.Trim());
        command.Parameters.AddWithValue("$last", patient.LastName.Trim());
        object birth = patient.DateOfBirth.HasValue ? patient.DateOfBirth.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : DBNull.Value;
        command.Parameters.AddWithValue("$birth", birth);
        command.Parameters.AddWithValue("$sex", (object?)patient.Sex ?? DBNull.Value);
        command.Parameters.AddWithValue("$phone", (object?)patient.PhoneNumber ?? DBNull.Value);
        command.Parameters.AddWithValue("$created", (patient.CreatedAtUtc == default ? DateTime.UtcNow : patient.CreatedAtUtc.ToUniversalTime()).ToString("O", CultureInfo.InvariantCulture));
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
