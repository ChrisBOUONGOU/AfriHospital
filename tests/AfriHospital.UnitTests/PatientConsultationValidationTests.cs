using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Infrastructure.Persistence;
using AfriHospital.src.Infrastructure.Repositories;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class PatientConsultationValidationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AfriHospitalPhase2Validation", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public PatientConsultationValidationTests()
    {
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "phase2.db");
    }

    [Fact]
    public async Task PatientRepository_RejectsMissingNameFutureBirthDateAndOversizedFields()
    {
        var repository = new SqlitePatientRepository(new SqliteConnectionFactory(_databasePath));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Patient
        {
            PatientNumber = "PAT-TEST-1",
            FirstName = " ",
            CreatedAtUtc = DateTime.UtcNow
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Patient
        {
            PatientNumber = "PAT-TEST-2",
            FirstName = "Awa",
            DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            CreatedAtUtc = DateTime.UtcNow
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Patient
        {
            PatientNumber = "PAT-TEST-3",
            FirstName = "Awa",
            LastName = "Diop",
            PhoneNumber = new string('1', 31),
            CreatedAtUtc = DateTime.UtcNow
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Patient
        {
            PatientNumber = "PAT-TEST-4",
            FirstName = "Awa",
            LastName = "Diop",
            Notes = new string('n', 2001),
            CreatedAtUtc = DateTime.UtcNow
        }));
    }

    [Fact]
    public async Task PatientRepository_TrimsAndPersistsValidPatientFields()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        var repository = new SqlitePatientRepository(factory);
        var patient = new Patient
        {
            PatientNumber = " PAT-VALID-1 ",
            FirstName = " Awa ",
            LastName = " Diop ",
            PhoneNumber = " +221700000000 ",
            DateOfBirth = new DateOnly(1990, 5, 2),
            Sex = " F ",
            Notes = " Note administrative ",
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(patient);

        var saved = await repository.GetByIdAsync(patient.Id);
        Assert.NotNull(saved);
        Assert.Equal("PAT-VALID-1", saved!.PatientNumber);
        Assert.Equal("Awa Diop", saved.FullName);
        Assert.Equal("+221700000000", saved.PhoneNumber);
        Assert.Equal("F", saved.Sex);
        Assert.Equal("Note administrative", saved.Notes);
    }

    [Fact]
    public async Task ConsultationRepository_RejectsMissingRequiredFieldsFutureDatesAndOversizedText()
    {
        var repository = new SqliteConsultationRepository(new SqliteConnectionFactory(_databasePath));
        var patientId = Guid.NewGuid();
        var practitionerId = Guid.NewGuid();
        var validDate = DateTime.UtcNow.AddMinutes(-1);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Consultation
        {
            PatientId = Guid.Empty,
            PractitionerId = practitionerId,
            ConsultationDateUtc = validDate,
            PractitionerName = "Dr Test",
            ClinicalNotes = "Motif"
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Consultation
        {
            PatientId = patientId,
            PractitionerId = Guid.Empty,
            ConsultationDateUtc = validDate,
            PractitionerName = "Dr Test",
            ClinicalNotes = "Motif"
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Consultation
        {
            PatientId = patientId,
            PractitionerId = practitionerId,
            ConsultationDateUtc = DateTime.UtcNow.AddDays(1),
            PractitionerName = "Dr Test",
            ClinicalNotes = "Motif"
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Consultation
        {
            PatientId = patientId,
            PractitionerId = practitionerId,
            ConsultationDateUtc = validDate,
            PractitionerName = "Dr Test",
            ClinicalNotes = " "
        }));

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddAsync(new Consultation
        {
            PatientId = patientId,
            PractitionerId = practitionerId,
            ConsultationDateUtc = validDate,
            PractitionerName = "Dr Test",
            ClinicalNotes = new string('x', 4001)
        }));
    }

    [Fact]
    public async Task ConsultationRepository_SavesValidConsultationAndReturnsPatientHistory()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        var patients = new SqlitePatientRepository(factory);
        var patient = new Patient
        {
            PatientNumber = "PAT-CONSULT-1",
            FirstName = "Mariam",
            LastName = "Diallo",
            CreatedAtUtc = DateTime.UtcNow
        };
        await patients.AddAsync(patient);

        var consultations = new SqliteConsultationRepository(factory);
        var consultation = new Consultation
        {
            PatientId = patient.Id,
            PractitionerId = Guid.NewGuid(),
            ConsultationDateUtc = DateTime.UtcNow.AddMinutes(-1),
            PractitionerName = " Dr Test ",
            ClinicalNotes = " Motif de consultation ",
            Diagnosis = " Diagnostic de test "
        };

        await consultations.AddAsync(consultation);

        Assert.NotEqual(Guid.Empty, consultation.Id);
        var saved = Assert.Single(await consultations.GetForPatientAsync(patient.Id));
        Assert.Equal(consultation.Id, saved.Id);
        Assert.Equal("Dr Test", saved.PractitionerName);
        Assert.Contains("Motif de consultation", saved.ClinicalNotes);
        Assert.Contains("Diagnostic de test", saved.ClinicalNotes);
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch { }
    }
}
