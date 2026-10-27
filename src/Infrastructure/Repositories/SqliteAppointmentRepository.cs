using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Domain.Interfaces;
using AfriHospital.src.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace AfriHospital.src.Infrastructure.Repositories;

/// <summary>
/// Repository for appointments. Patient references in the historical table are resolved
/// through the GUID patient projection, preserving compatibility with existing local data.
/// </summary>
public sealed class SqliteAppointmentRepository(SqliteConnectionFactory factory) : IAppointmentRepository
{
    private static readonly string[] DateFormats = ["yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss"];

    public async Task<IReadOnlyList<Appointment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await factory.InitializeAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);
        var items = new List<Appointment>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT a.id, dp.id, COALESCE(lp.name, a.patient_name), a.scheduled_at, a.reason, a.status
            FROM appointments a
            LEFT JOIN patients lp ON lp.id=a.patient_id
            LEFT JOIN domain_patients dp ON dp.legacy_patient_id=a.patient_id
            ORDER BY a.scheduled_at DESC, a.id DESC;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var scheduled = DateTime.ParseExact(reader.GetString(3), DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None);
            items.Add(new Appointment
            {
                Id = reader.GetInt64(0),
                PatientId = reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)),
                PatientName = reader.GetString(2),
                ScheduledAt = DateTime.SpecifyKind(scheduled, DateTimeKind.Unspecified),
                Reason = reader.GetString(4),
                Status = reader.GetString(5)
            });
        }
        return items;
    }

    public async Task<bool> HasConflictAsync(Guid patientId, DateTime scheduledAt, long? excludingAppointmentId = null, CancellationToken cancellationToken = default)
    {
        await factory.InitializeAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS(
                SELECT 1 FROM appointments a
                JOIN domain_patients dp ON dp.legacy_patient_id=a.patient_id
                WHERE dp.id=$patientId AND a.scheduled_at=$scheduledAt
                  AND a.status <> 'Annulé'
                  AND ($excludeId IS NULL OR a.id <> $excludeId)
            );
            """;
        command.Parameters.AddWithValue("$patientId", patientId.ToString("D"));
        command.Parameters.AddWithValue("$scheduledAt", scheduledAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$excludeId", (object?)excludingAppointmentId ?? DBNull.Value);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) != 0;
    }

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        Validate(appointment, requireId: false);
        await factory.InitializeAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO appointments(patient_id, patient_name, scheduled_at, reason, status)
            SELECT dp.legacy_patient_id, lp.name, $scheduledAt, $reason, 'Planifié'
            FROM domain_patients dp JOIN patients lp ON lp.id=dp.legacy_patient_id
            WHERE dp.id=$patientId AND dp.legacy_patient_id IS NOT NULL;
            """;
        Bind(command, appointment);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new KeyNotFoundException("Le patient lié à ce rendez-vous est introuvable.");
        await using var idCommand = connection.CreateCommand();
        idCommand.CommandText = "SELECT last_insert_rowid();";
        appointment.Id = Convert.ToInt64(await idCommand.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
        appointment.Status = "Planifié";
    }

    public async Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointment);
        Validate(appointment, requireId: true);
        await factory.InitializeAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE appointments
            SET patient_id=(SELECT legacy_patient_id FROM domain_patients WHERE id=$patientId),
                patient_name=(SELECT name FROM patients WHERE id=(SELECT legacy_patient_id FROM domain_patients WHERE id=$patientId)),
                scheduled_at=$scheduledAt, reason=$reason, status=$status
            WHERE id=$id AND EXISTS(SELECT 1 FROM domain_patients WHERE id=$patientId AND legacy_patient_id IS NOT NULL);
            """;
        Bind(command, appointment);
        command.Parameters.AddWithValue("$status", appointment.Status);
        command.Parameters.AddWithValue("$id", appointment.Id);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new KeyNotFoundException("Rendez-vous ou patient introuvable.");
    }

    public async Task CancelAsync(long appointmentId, CancellationToken cancellationToken = default)
    {
        if (appointmentId <= 0) throw new ArgumentOutOfRangeException(nameof(appointmentId));
        await factory.InitializeAsync(cancellationToken);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE appointments SET status='Annulé' WHERE id=$id AND status <> 'Annulé';";
        command.Parameters.AddWithValue("$id", appointmentId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void Validate(Appointment appointment, bool requireId)
    {
        if (requireId && appointment.Id <= 0) throw new ArgumentException("L'identifiant du rendez-vous est invalide.");
        if (appointment.PatientId is null || appointment.PatientId == Guid.Empty) throw new ArgumentException("Un patient doit être sélectionné.");
        if (appointment.ScheduledAt == default) throw new ArgumentException("La date du rendez-vous est obligatoire.");
        if ((appointment.Reason?.Trim().Length ?? 0) > 250) throw new ArgumentException("Le motif ne doit pas dépasser 250 caractères.");
        if (string.IsNullOrWhiteSpace(appointment.Status)) appointment.Status = "Planifié";
    }

    private static void Bind(SqliteCommand command, Appointment appointment)
    {
        command.Parameters.AddWithValue("$patientId", appointment.PatientId!.Value.ToString("D"));
        command.Parameters.AddWithValue("$scheduledAt", appointment.ScheduledAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$reason", appointment.Reason?.Trim() ?? string.Empty);
    }
}
