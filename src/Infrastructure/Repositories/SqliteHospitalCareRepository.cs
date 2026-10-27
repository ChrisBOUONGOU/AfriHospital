using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Domain.Interfaces;
using AfriHospital.src.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace AfriHospital.src.Infrastructure.Repositories;

/// <summary>Persistence for admissions, bed occupancy and laboratory workflows.</summary>
public sealed class SqliteHospitalCareRepository(SqliteConnectionFactory factory) : IHospitalCareRepository
{
    public async Task<Guid> AdmitAsync(Guid patientId, Guid bedId, DateTime admittedAtUtc, string service, string reason, CancellationToken cancellationToken = default)
    {
        if (patientId == Guid.Empty || bedId == Guid.Empty) throw new ArgumentException("Le patient et le lit sont obligatoires.");
        if (admittedAtUtc == default || admittedAtUtc > DateTime.UtcNow.AddMinutes(5)) throw new ArgumentException("La date d'admission est invalide.");
        service = Required(service, 120, nameof(service));
        reason = Optional(reason, 1000, nameof(reason));
        await using var connection = await OpenAsync(cancellationToken);
        await using var tx = await connection.BeginTransactionAsync(cancellationToken);
        await using (var check = connection.CreateCommand())
        {
            check.Transaction = (SqliteTransaction)tx;
            check.CommandText = """
                SELECT (SELECT COUNT(*) FROM domain_patients WHERE id=$patient),
                       (SELECT COUNT(*) FROM hospital_beds WHERE id=$bed AND is_active=1),
                       (SELECT COUNT(*) FROM hospital_admissions WHERE bed_id=$bed AND discharged_at_utc IS NULL);
                """;
            check.Parameters.AddWithValue("$patient", patientId.ToString("D"));
            check.Parameters.AddWithValue("$bed", bedId.ToString("D"));
            await using var reader = await check.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            if (reader.GetInt64(0) == 0) throw new KeyNotFoundException("Patient introuvable.");
            if (reader.GetInt64(1) == 0) throw new KeyNotFoundException("Lit actif introuvable.");
            if (reader.GetInt64(2) != 0) throw new InvalidOperationException("Ce lit est déjà occupé.");
        }
        var id = Guid.NewGuid();
        await using var insert = connection.CreateCommand();
        insert.Transaction = (SqliteTransaction)tx;
        insert.CommandText = "INSERT INTO hospital_admissions(id,patient_id,bed_id,admitted_at_utc,service,reason) VALUES($id,$patient,$bed,$date,$service,$reason);";
        insert.Parameters.AddWithValue("$id", id.ToString("D"));
        insert.Parameters.AddWithValue("$patient", patientId.ToString("D"));
        insert.Parameters.AddWithValue("$bed", bedId.ToString("D"));
        insert.Parameters.AddWithValue("$date", admittedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        insert.Parameters.AddWithValue("$service", service);
        insert.Parameters.AddWithValue("$reason", reason);
        await insert.ExecuteNonQueryAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return id;
    }

    public async Task DischargeAsync(Guid admissionId, DateTime dischargedAtUtc, CancellationToken cancellationToken = default)
    {
        if (admissionId == Guid.Empty || dischargedAtUtc == default) throw new ArgumentException("L'admission et la date de sortie sont obligatoires.");
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE hospital_admissions SET discharged_at_utc=$date
            WHERE id=$id AND discharged_at_utc IS NULL
              AND $date >= admitted_at_utc;
            """;
        command.Parameters.AddWithValue("$id", admissionId.ToString("D"));
        command.Parameters.AddWithValue("$date", dischargedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("Admission introuvable, déjà clôturée ou date de sortie antérieure à l'admission.");
    }

    public async Task<IReadOnlyList<HospitalAdmission>> GetAdmissionsAsync(bool includeDischarged = true, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,patient_id,admitted_at_utc,service,reason,discharged_at_utc
            FROM hospital_admissions WHERE $all=1 OR discharged_at_utc IS NULL ORDER BY admitted_at_utc DESC;
            """;
        command.Parameters.AddWithValue("$all", includeDischarged ? 1 : 0);
        var rows = new List<HospitalAdmission>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), reader.GetString(3), reader.GetString(4), reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        return rows;
    }

    public async Task<Guid> AddBedAsync(HospitalBed bed, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bed);
        var ward = Required(bed.Ward, 100, nameof(bed.Ward));
        var room = Required(bed.RoomNumber, 30, nameof(bed.RoomNumber));
        var number = Required(bed.BedNumber, 30, nameof(bed.BedNumber));
        await using var connection = await OpenAsync(cancellationToken);
        var id = bed.Id == Guid.Empty ? Guid.NewGuid() : bed.Id;
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO hospital_beds(id,ward,room_number,bed_number,is_active) VALUES($id,$ward,$room,$bed,$active);";
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$ward", ward);
        command.Parameters.AddWithValue("$room", room);
        command.Parameters.AddWithValue("$bed", number);
        command.Parameters.AddWithValue("$active", bed.IsActive ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return id;
    }

    public async Task<IReadOnlyList<HospitalBed>> GetAvailableBedsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT b.id,b.ward,b.room_number,b.bed_number,b.is_active FROM hospital_beds b
            WHERE b.is_active=1 AND NOT EXISTS (
              SELECT 1 FROM hospital_admissions a WHERE a.bed_id=b.id AND a.discharged_at_utc IS NULL)
            ORDER BY b.ward,b.room_number,b.bed_number;
            """;
        var rows = new List<HospitalBed>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) rows.Add(new(Guid.Parse(reader.GetString(0)), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt64(4) == 1));
        return rows;
    }

    public async Task<Guid> RequestLabTestAsync(Guid patientId, Guid? consultationId, string testName, string requestedBy, CancellationToken cancellationToken = default)
    {
        if (patientId == Guid.Empty) throw new ArgumentException("Le patient est obligatoire.");
        testName = Required(testName, 160, nameof(testName));
        requestedBy = Required(requestedBy, 150, nameof(requestedBy));
        await using var connection = await OpenAsync(cancellationToken);
        var id = Guid.NewGuid();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO laboratory_orders(id,patient_id,consultation_id,test_name,requested_by,requested_at_utc,status)
            SELECT $id,$patient,$consultation,$test,$requester,$date,'Demandée'
            WHERE EXISTS(SELECT 1 FROM domain_patients WHERE id=$patient);
            """;
        command.Parameters.AddWithValue("$id", id.ToString("D"));
        command.Parameters.AddWithValue("$patient", patientId.ToString("D"));
        command.Parameters.AddWithValue("$consultation", consultationId is null ? DBNull.Value : consultationId.Value.ToString("D"));
        command.Parameters.AddWithValue("$test", testName);
        command.Parameters.AddWithValue("$requester", requestedBy);
        command.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new KeyNotFoundException("Patient introuvable.");
        return id;
    }

    public async Task RecordLabResultAsync(Guid orderId, string result, CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("La demande d'analyse est obligatoire.");
        result = Required(result, 4000, nameof(result));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE laboratory_orders SET result=$result,resulted_at_utc=$date,status='Résultat disponible' WHERE id=$id AND status='Demandée';";
        command.Parameters.AddWithValue("$result", result);
        command.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$id", orderId.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) throw new InvalidOperationException("Demande introuvable ou résultat déjà enregistré.");
    }

    public async Task<IReadOnlyList<LaboratoryOrder>> GetLabOrdersAsync(Guid? patientId = null, CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id,patient_id,consultation_id,test_name,requested_by,requested_at_utc,status,result,resulted_at_utc
            FROM laboratory_orders WHERE $patient IS NULL OR patient_id=$patient ORDER BY requested_at_utc DESC;
            """;
        command.Parameters.AddWithValue("$patient", patientId is null ? DBNull.Value : patientId.Value.ToString("D"));
        var rows = new List<LaboratoryOrder>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            rows.Add(new(Guid.Parse(reader.GetString(0)), Guid.Parse(reader.GetString(1)), reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)), reader.GetString(3), reader.GetString(4), DateTime.Parse(reader.GetString(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : DateTime.Parse(reader.GetString(8), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)));
        return rows;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await factory.InitializeAsync(cancellationToken);
        var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS hospital_beds(
              id TEXT PRIMARY KEY, ward TEXT NOT NULL, room_number TEXT NOT NULL, bed_number TEXT NOT NULL,
              is_active INTEGER NOT NULL DEFAULT 1 CHECK(is_active IN (0,1)),
              UNIQUE(ward,room_number,bed_number));
            CREATE TABLE IF NOT EXISTS hospital_admissions(
              id TEXT PRIMARY KEY, patient_id TEXT NOT NULL REFERENCES domain_patients(id),
              bed_id TEXT NOT NULL REFERENCES hospital_beds(id), admitted_at_utc TEXT NOT NULL,
              service TEXT NOT NULL, reason TEXT NOT NULL DEFAULT '', discharged_at_utc TEXT NULL);
            CREATE INDEX IF NOT EXISTS ix_admissions_patient ON hospital_admissions(patient_id,admitted_at_utc);
            CREATE INDEX IF NOT EXISTS ix_admissions_bed_active ON hospital_admissions(bed_id,discharged_at_utc);
            CREATE TABLE IF NOT EXISTS laboratory_orders(
              id TEXT PRIMARY KEY, patient_id TEXT NOT NULL REFERENCES domain_patients(id),
              consultation_id TEXT NULL, test_name TEXT NOT NULL, requested_by TEXT NOT NULL,
              requested_at_utc TEXT NOT NULL, status TEXT NOT NULL CHECK(status IN ('Demandée','Résultat disponible')),
              result TEXT NULL, resulted_at_utc TEXT NULL);
            CREATE INDEX IF NOT EXISTS ix_lab_patient_date ON laboratory_orders(patient_id,requested_at_utc);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static string Required(string value, int max, string name)
    {
        value = value?.Trim() ?? "";
        if (value.Length == 0 || value.Length > max) throw new ArgumentException($"Champ obligatoire, limité à {max} caractères.", name);
        return value;
    }
    private static string Optional(string? value, int max, string name)
    {
        value = value?.Trim() ?? "";
        if (value.Length > max) throw new ArgumentException($"Champ limité à {max} caractères.", name);
        return value;
    }
}
