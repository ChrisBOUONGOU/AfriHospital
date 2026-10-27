using AfriHospital.src.Infrastructure.Persistence;
using AfriHospital.src.Infrastructure.Repositories;
using AfriHospital.src.Domain.Entities;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class LegacyDomainDataMigrationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AfriHospitalMigrationTests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public LegacyDomainDataMigrationTests()
    {
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "migration.db");
    }

    [Fact]
    public async Task Initialize_ImportsLegacyPatientsAndConsultations_WithoutDuplicatesOrChangingLegacyRows()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        await factory.InitializeAsync();

        await using (var connection = factory.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO patients(id,name,phone,birth_date,gender,notes,created_at)
                VALUES(41,'Amina Diallo','+221700000000','1992-03-14','F','Notes historiques à conserver','2024-01-02 03:04:05');
                INSERT INTO consultations(id,patient_id,patient_name,consulted_at,clinician,summary,created_at)
                VALUES(73,41,'Amina Diallo','2024-02-03 10:30:00','Dr. Test','Résumé historique','2024-02-03 10:30:00');
                """;
            await command.ExecuteNonQueryAsync();
        }

        await factory.InitializeAsync();
        await factory.InitializeAsync();

        await using (var connection = factory.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    (SELECT COUNT(*) FROM domain_patients WHERE legacy_patient_id=41),
                    (SELECT COUNT(*) FROM domain_consultations WHERE legacy_consultation_id=73),
                    (SELECT COUNT(*) FROM patients WHERE id=41 AND notes='Notes historiques à conserver'),
                    (SELECT COUNT(*) FROM consultations WHERE id=73 AND summary='Résumé historique'),
                    (SELECT first_name || '|' || last_name || '|' || phone_number FROM domain_patients WHERE legacy_patient_id=41),
                    (SELECT clinical_notes FROM domain_consultations WHERE legacy_consultation_id=73);
                """;
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(1L, reader.GetInt64(0));
            Assert.Equal(1L, reader.GetInt64(1));
            Assert.Equal(1L, reader.GetInt64(2));
            Assert.Equal(1L, reader.GetInt64(3));
            Assert.Equal("Amina|Diallo|+221700000000", reader.GetString(4));
            Assert.Equal("Résumé historique", reader.GetString(5));
        }
    }

    [Fact]
    public async Task Initialize_AddsMissingLegacyConsultationPatientIdBeforeImport()
    {
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE patients(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    name TEXT NOT NULL,
                    phone TEXT NOT NULL DEFAULT '',
                    birth_date TEXT NOT NULL DEFAULT '',
                    gender TEXT NOT NULL DEFAULT '',
                    notes TEXT NOT NULL DEFAULT '',
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                CREATE TABLE consultations(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    patient_name TEXT NOT NULL,
                    consulted_at TEXT NOT NULL,
                    clinician TEXT NOT NULL DEFAULT '',
                    summary TEXT NOT NULL DEFAULT '',
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                INSERT INTO patients(id,name) VALUES(5,'Legacy Patient');
                INSERT INTO consultations(id,patient_name,consulted_at,summary)
                VALUES(9,'Legacy Patient','2023-01-01 09:00:00','Legacy note');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var factory = new SqliteConnectionFactory(_databasePath);
        await factory.InitializeAsync();

        await using var check = factory.CreateConnection();
        await check.OpenAsync();
        await using var schema = check.CreateCommand();
        schema.CommandText = "PRAGMA table_info(consultations)";
        await using var reader = await schema.ExecuteReaderAsync();
        var hasPatientId = false;
        while (await reader.ReadAsync())
            if (reader.GetString(1).Equals("patient_id", StringComparison.OrdinalIgnoreCase))
                hasPatientId = true;
        Assert.True(hasPatientId);
    }

    [Fact]
    public async Task DomainRepositories_WriteToSharedLegacyTablesAndCanReadTheSameRecords()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        await factory.InitializeAsync();
        var patients = new SqlitePatientRepository(factory);
        var patient = new Patient
        {
            PatientNumber = "AH-0001",
            FirstName = "Marie",
            LastName = "N'Diaye",
            DateOfBirth = new DateOnly(1990, 5, 2),
            Sex = "F",
            PhoneNumber = "555-0100",
            Notes = "Notes administratives",
            CreatedAtUtc = DateTime.UtcNow
        };

        await patients.AddAsync(patient);
        Assert.NotEqual(Guid.Empty, patient.Id);

        await using (var connection = factory.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM patients WHERE patient_number='AH-0001' AND name='Marie N''Diaye';";
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        }

        patient.PhoneNumber = "555-0101";
        patient.PatientNumber = "AH-0002";
        await patients.UpdateAsync(patient);
        var found = await patients.GetByIdAsync(patient.Id);
        Assert.NotNull(found);
        Assert.Equal("555-0101", found!.PhoneNumber);
        Assert.Equal("AH-0002", found.PatientNumber);
        Assert.Equal("Notes administratives", found.Notes);

        var consultations = new SqliteConsultationRepository(factory);
        var practitionerId = Guid.NewGuid();
        var consultation = new Consultation
        {
            PatientId = patient.Id,
            PractitionerId = practitionerId,
            ConsultationDateUtc = DateTime.UtcNow,
            ClinicalNotes = "Motif de consultation",
            Diagnosis = "Diagnostic de test",
            PractitionerName = "Dr. Test"
        };
        await consultations.AddAsync(consultation);
        Assert.NotEqual(Guid.Empty, consultation.Id);

        await using (var connection = factory.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM consultations WHERE patient_id=(SELECT legacy_patient_id FROM domain_patients WHERE id=$id) AND summary LIKE '%Diagnostic de test%';";
            command.Parameters.AddWithValue("$id", patient.Id.ToString("D"));
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
        }

        var history = await consultations.GetForPatientAsync(patient.Id);
        Assert.Contains(history, item => item.Id == consultation.Id && item.PractitionerName == "Dr. Test");
        var allConsultations = await consultations.GetAllAsync();
        Assert.Contains(allConsultations, item => item.Id == consultation.Id && item.PatientId == patient.Id);
    }

    [Fact]
    public async Task Initialize_ExportsExistingGuidOnlyRecordsToSharedLegacyTables()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        await factory.InitializeAsync();
        var patientId = Guid.NewGuid();
        var consultationId = Guid.NewGuid();
        var practitionerId = Guid.NewGuid();

        await using (var connection = factory.CreateConnection())
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO domain_patients(id,patient_number,first_name,last_name,date_of_birth,sex,phone_number,created_at_utc)
                VALUES($patient,'GUID-0001','Sara','Martin','1988-06-07','F','555-0200','2022-03-04T00:00:00.0000000Z');
                INSERT INTO domain_consultations(id,patient_id,practitioner_id,consultation_date_utc,clinical_notes,diagnosis)
                VALUES($consultation,$patient,$practitioner,'2023-04-05T12:00:00.0000000Z','Douleur abdominale','Gastrite');
                """;
            command.Parameters.AddWithValue("$patient", patientId.ToString("D"));
            command.Parameters.AddWithValue("$consultation", consultationId.ToString("D"));
            command.Parameters.AddWithValue("$practitioner", practitionerId.ToString("D"));
            await command.ExecuteNonQueryAsync();
        }

        await factory.InitializeAsync();

        await using var verify = factory.CreateConnection();
        await verify.OpenAsync();
        await using var query = verify.CreateCommand();
        query.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM patients WHERE patient_number='GUID-0001' AND name='Sara Martin'),
                (SELECT COUNT(*) FROM consultations WHERE summary LIKE '%Douleur abdominale%' AND summary LIKE '%Gastrite%'),
                (SELECT id FROM domain_patients WHERE patient_number='GUID-0001'),
                (SELECT id FROM domain_consultations WHERE legacy_consultation_id=(SELECT id FROM consultations WHERE summary LIKE '%Gastrite%'));
            """;
        await using var reader = await query.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1L, reader.GetInt64(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.Equal(patientId.ToString("D"), reader.GetString(2));
        Assert.Equal(consultationId.ToString("D"), reader.GetString(3));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
