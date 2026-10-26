using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Domain.Interfaces;
using AfriHospital.src.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AfriHospital.src.Infrastructure.Repositories;

public sealed class SqliteHospitalUserRepository(SqliteConnectionFactory factory) : IHospitalUserRepository
{
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM hospital_users;";
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task CreateInitialAdministratorAsync(HospitalUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
        user.Role = "Admin";
        Validate(user);

        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await using (var count = connection.CreateCommand())
        {
            count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM hospital_users;";
            if (Convert.ToInt32(await count.ExecuteScalarAsync(cancellationToken)) != 0)
                throw new InvalidOperationException("Le compte administrateur initial a déjà été créé.");
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO hospital_users(id, username, password_hash, role, is_active) VALUES($id,$username,$hash,'Admin',1);";
            Bind(command, user);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        transaction.Commit();
    }

    public async Task<HospitalUser?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username)) return null;
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, username, password_hash, role, is_active FROM hospital_users WHERE username=$username COLLATE NOCASE LIMIT 1;";
        command.Parameters.AddWithValue("$username", username.Trim());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? new HospitalUser
        {
            Id = Guid.Parse(reader.GetString(0)), Username = reader.GetString(1),
            PasswordHash = reader.GetString(2), Role = reader.GetString(3), IsActive = reader.GetInt32(4) == 1
        } : null;
    }

    public async Task AddAsync(HospitalUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
        Validate(user);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO hospital_users(id, username, password_hash, role, is_active) VALUES($id,$username,$hash,$role,$active);";
        Bind(command, user);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateLastKnownStateAsync(HospitalUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.Id == Guid.Empty) throw new ArgumentException("L'identifiant utilisateur est invalide.");
        Validate(user);
        await using var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE hospital_users SET username=$username,password_hash=$hash,role=$role,is_active=$active WHERE id=$id;";
        Bind(command, user);
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            throw new KeyNotFoundException("Utilisateur hospitalier introuvable.");
    }

    private static void Validate(HospitalUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Username) || string.IsNullOrWhiteSpace(user.PasswordHash) || string.IsNullOrWhiteSpace(user.Role))
            throw new ArgumentException("Le nom d'utilisateur, le hash du mot de passe et le rôle sont obligatoires.");
    }

    private static void Bind(SqliteCommand command, HospitalUser user)
    {
        command.Parameters.AddWithValue("$id", user.Id.ToString("D"));
        command.Parameters.AddWithValue("$username", user.Username.Trim());
        command.Parameters.AddWithValue("$hash", user.PasswordHash);
        command.Parameters.AddWithValue("$role", user.Role.Trim());
        command.Parameters.AddWithValue("$active", user.IsActive ? 1 : 0);
    }
}
