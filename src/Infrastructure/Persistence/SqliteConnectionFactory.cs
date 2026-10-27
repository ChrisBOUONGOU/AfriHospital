using Microsoft.Data.Sqlite;

namespace AfriHospital.src.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string? databasePath = null)
    {
        databasePath ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AfriHospital",
            "afrihospital.db");

        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(_connectionString);

    public async Task WriteAuditAsync(string actor, string action, string details, CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO security_audit_log(actor, action, details, created_at_utc) VALUES($actor, $action, $details, $created);";
        command.Parameters.AddWithValue("$actor", string.IsNullOrWhiteSpace(actor) ? "unknown" : actor.Trim());
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$details", details);
        command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            CREATE TABLE IF NOT EXISTS patients (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT NOT NULL,
                patient_number TEXT NOT NULL DEFAULT '',
                phone TEXT NOT NULL DEFAULT '',
                birth_date TEXT NOT NULL DEFAULT '',
                gender TEXT NOT NULL DEFAULT '',
                notes TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE IF NOT EXISTS consultations (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                patient_id INTEGER NULL REFERENCES patients(id),
                patient_name TEXT NOT NULL,
                consulted_at TEXT NOT NULL,
                clinician TEXT NOT NULL DEFAULT '',
                summary TEXT NOT NULL DEFAULT '',
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
            CREATE TABLE IF NOT EXISTS domain_patients (
                id TEXT PRIMARY KEY NOT NULL,
                patient_number TEXT NOT NULL UNIQUE,
                first_name TEXT NOT NULL,
                last_name TEXT NOT NULL,
                date_of_birth TEXT NULL,
                sex TEXT NULL,
                phone_number TEXT NULL,
                created_at_utc TEXT NOT NULL,
                legacy_patient_id INTEGER NULL UNIQUE
            );
            CREATE INDEX IF NOT EXISTS ix_domain_patients_name
                ON domain_patients(last_name, first_name);
            CREATE TABLE IF NOT EXISTS domain_consultations (
                id TEXT PRIMARY KEY NOT NULL,
                patient_id TEXT NOT NULL REFERENCES domain_patients(id),
                practitioner_id TEXT NOT NULL,
                consultation_date_utc TEXT NOT NULL,
                clinical_notes TEXT NULL,
                diagnosis TEXT NULL,
                legacy_consultation_id INTEGER NULL UNIQUE
            );
            CREATE INDEX IF NOT EXISTS ix_domain_consultations_patient_date
                ON domain_consultations(patient_id, consultation_date_utc);
            CREATE TABLE IF NOT EXISTS security_audit_log (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                actor TEXT NOT NULL,
                action TEXT NOT NULL,
                details TEXT NOT NULL DEFAULT '',
                created_at_utc TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_security_audit_log_date ON security_audit_log(created_at_utc);
            CREATE TABLE IF NOT EXISTS hospital_users (
                id TEXT PRIMARY KEY NOT NULL,
                username TEXT NOT NULL COLLATE NOCASE UNIQUE,
                password_hash TEXT NOT NULL,
                role TEXT NOT NULL,
                is_active INTEGER NOT NULL DEFAULT 1
            );
            CREATE TRIGGER IF NOT EXISTS trg_security_audit_no_update
                BEFORE UPDATE ON security_audit_log BEGIN SELECT RAISE(ABORT, 'security audit log is append-only'); END;
            CREATE TRIGGER IF NOT EXISTS trg_security_audit_no_delete
                BEFORE DELETE ON security_audit_log BEGIN SELECT RAISE(ABORT, 'security audit log is append-only'); END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await EnsureColumnAsync(connection, "patients", "patient_number", "TEXT NOT NULL DEFAULT ''");
        await EnsureColumnAsync(connection, "consultations", "patient_id", "INTEGER NULL REFERENCES patients(id)");
        await EnsureColumnAsync(connection, "domain_patients", "legacy_patient_id", "INTEGER NULL");
        await EnsureColumnAsync(connection, "domain_consultations", "legacy_consultation_id", "INTEGER NULL");
        await using (var migrationIndexes = connection.CreateCommand())
        {
            migrationIndexes.CommandText = """
                CREATE UNIQUE INDEX IF NOT EXISTS ux_domain_patients_legacy_id ON domain_patients(legacy_patient_id);
                CREATE UNIQUE INDEX IF NOT EXISTS ux_domain_consultations_legacy_id ON domain_consultations(legacy_consultation_id);
                """;
            await migrationIndexes.ExecuteNonQueryAsync(cancellationToken);
        }
        await LegacyDomainDataMigration.ImportAsync(connection, cancellationToken);
    }

    private static async Task EnsureColumnAsync(SqliteConnection connection, string table, string column, string definition)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await check.ExecuteReaderAsync(cancellationToken: CancellationToken.None);
        while (await reader.ReadAsync())
            if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                return;
        await reader.DisposeAsync();
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync();
    }
}
