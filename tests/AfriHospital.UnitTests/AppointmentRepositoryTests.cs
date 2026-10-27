using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Infrastructure.Persistence;
using AfriHospital.src.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class AppointmentRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AfriHospitalAppointmentTests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public AppointmentRepositoryTests()
    {
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "appointments.db");
    }

    [Fact]
    public async Task AppointmentRepository_CreatesReadsUpdatesAndCancelsAppointmentsUsingDomainPatientId()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        await factory.InitializeAsync();
        var patientRepository = new SqlitePatientRepository(factory);
        var patient = new Patient
        {
            PatientNumber = "APT-0001",
            FirstName = "Awa",
            LastName = "Diop",
            CreatedAtUtc = DateTime.UtcNow
        };
        await patientRepository.AddAsync(patient);

        var repository = new SqliteAppointmentRepository(factory);
        var scheduledAt = new DateTime(2026, 11, 5, 9, 30, 0, DateTimeKind.Unspecified);
        var appointment = new Appointment
        {
            PatientId = patient.Id,
            PatientName = patient.FullName,
            ScheduledAt = scheduledAt,
            Reason = "Consultation de suivi"
        };

        await repository.AddAsync(appointment);
        Assert.True(appointment.Id > 0);
        Assert.True(await repository.HasConflictAsync(patient.Id, scheduledAt));

        var rows = await repository.GetAllAsync();
        var saved = Assert.Single(rows, item => item.Id == appointment.Id);
        Assert.Equal(patient.Id, saved.PatientId);
        Assert.Equal(patient.FullName, saved.PatientName);
        Assert.Equal("Planifié", saved.Status);
        Assert.Equal("Consultation de suivi", saved.Reason);

        var updatedDate = scheduledAt.AddHours(1);
        saved.ScheduledAt = updatedDate;
        saved.Reason = "Suivi modifié";
        saved.Status = "Confirmé";
        await repository.UpdateAsync(saved);

        Assert.False(await repository.HasConflictAsync(patient.Id, scheduledAt));
        Assert.True(await repository.HasConflictAsync(patient.Id, updatedDate));
        Assert.False(await repository.HasConflictAsync(patient.Id, updatedDate, saved.Id));
        var updated = Assert.Single(await repository.GetAllAsync(), item => item.Id == saved.Id);
        Assert.Equal("Suivi modifié", updated.Reason);
        Assert.Equal("Confirmé", updated.Status);

        await repository.CancelAsync(saved.Id);
        Assert.False(await repository.HasConflictAsync(patient.Id, updatedDate));
        var cancelled = Assert.Single(await repository.GetAllAsync(), item => item.Id == saved.Id);
        Assert.Equal("Annulé", cancelled.Status);

        await using var connection = factory.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM appointments WHERE id=$id AND patient_id=(SELECT legacy_patient_id FROM domain_patients WHERE id=$patientId);";
        command.Parameters.AddWithValue("$id", saved.Id);
        command.Parameters.AddWithValue("$patientId", patient.Id.ToString("D"));
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task AppointmentRepository_RejectsAppointmentWithoutPatient()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        await factory.InitializeAsync();
        var repository = new SqliteAppointmentRepository(factory);
        var appointment = new Appointment
        {
            ScheduledAt = new DateTime(2026, 11, 5, 9, 30, 0),
            Reason = "No patient"
        };
        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(appointment));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch { }
    }
}
