using AfriHospital;
using AfriHospital.src.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class AuditLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AfriHospitalAuditTests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public AuditLogTests()
    {
        Directory.CreateDirectory(_directory);
        _databasePath = Path.Combine(_directory, "audit.db");
    }

    [Fact]
    public async Task BusinessAudit_StoresActorActionAndDetails()
    {
        var database = new HospitalDatabase(_databasePath);
        await database.InitializeAsync();
        await database.WriteAuditAsync("INVOICE_CREATED", "invoice", "billing-user", "invoice_id=12");

        var row = Assert.Single(await database.GetRowsAsync(
            "SELECT actor || '|' || action || '|' || entity || '|' || details FROM audit_log",
            reader => reader.GetString(0)));
        Assert.Equal("billing-user|INVOICE_CREATED|invoice|invoice_id=12", row);
    }

    [Fact]
    public async Task BusinessAuditLog_RejectsUpdatesAndDeletes()
    {
        var database = new HospitalDatabase(_databasePath);
        await database.InitializeAsync();
        await database.WriteAuditAsync("INVOICE_CREATED", "invoice", "billing-user", "invoice_id=12");

        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteAsync("UPDATE audit_log SET details='tampered'"));
        await Assert.ThrowsAsync<SqliteException>(() => database.ExecuteAsync("DELETE FROM audit_log"));

        var row = Assert.Single(await database.GetRowsAsync(
            "SELECT details FROM audit_log", reader => reader.GetString(0)));
        Assert.Equal("invoice_id=12", row);
    }

    [Fact]
    public async Task SecurityAuditLog_RejectsUpdatesAndDeletes()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        await factory.InitializeAsync();
        await factory.WriteAuditAsync("admin", "LOGIN_SUCCESS", "test");

        await using var connection = factory.CreateConnection();
        await connection.OpenAsync();
        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE security_audit_log SET details='tampered'";
        await Assert.ThrowsAsync<SqliteException>(async () => await update.ExecuteNonQueryAsync());

        await using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM security_audit_log";
        await Assert.ThrowsAsync<SqliteException>(async () => await delete.ExecuteNonQueryAsync());
    }

    [Fact]
    public async Task Initialize_MigratesExistingAuditLogWithoutLosingRows()
    {
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _databasePath }.ToString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE audit_log(
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    action TEXT NOT NULL,
                    entity TEXT NOT NULL,
                    created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
                INSERT INTO audit_log(action,entity) VALUES('PATIENT_CREATED','patient');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var database = new HospitalDatabase(_databasePath);
        await database.InitializeAsync();
        var row = Assert.Single(await database.GetRowsAsync(
            "SELECT actor || '|' || details || '|' || action FROM audit_log",
            reader => reader.GetString(0)));
        Assert.Equal("unknown||PATIENT_CREATED", row);
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
