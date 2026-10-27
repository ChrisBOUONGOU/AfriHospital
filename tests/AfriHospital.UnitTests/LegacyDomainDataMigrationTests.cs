using AfriHospital.src.Infrastructure.Persistence;
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

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
