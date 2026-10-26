using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Domain.Interfaces;
using AfriHospital.src.Infrastructure.Persistence;
using AfriHospital.src.Infrastructure.Security;

namespace AfriHospital.src.Application.Services;

/// <summary>
/// Authentication primitives. The desktop UI must call this service before
/// exposing protected modules; creating this service alone does not enforce UI access.
/// </summary>
public sealed class AuthenticationService(
    SqliteConnectionFactory connectionFactory,
    IHospitalUserRepository users,
    PasswordHasher passwordHasher)
{
    public async Task<int> CountUsersAsync(CancellationToken cancellationToken = default)
    {
        await connectionFactory.InitializeAsync(cancellationToken);
        return await users.CountAsync(cancellationToken);
    }

    public async Task<HospitalUser> CreateInitialAdministratorAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Le nom d'utilisateur est obligatoire.", nameof(username));
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new ArgumentException("Le mot de passe doit contenir au moins 12 caractères.", nameof(password));

        await connectionFactory.InitializeAsync(cancellationToken);
        var user = new HospitalUser
        {
            Id = Guid.NewGuid(),
            Username = username.Trim(),
            PasswordHash = passwordHasher.HashPassword(password),
            Role = "Admin",
            IsActive = true
        };
        await users.CreateInitialAdministratorAsync(user, cancellationToken);
        await connectionFactory.WriteAuditAsync(user.Username, "INITIAL_ADMIN_CREATED", "First administrator account created.", cancellationToken);
        return user;
    }

    public async Task<HospitalUser?> AuthenticateAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return null;

        await connectionFactory.InitializeAsync(cancellationToken);
        var user = await users.GetByUsernameAsync(username, cancellationToken);
        if (user is null || !user.IsActive ||
            !passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            await connectionFactory.WriteAuditAsync(username.Trim(), "LOGIN_FAILED", "Credentials rejected or account inactive.", cancellationToken);
            return null;
        }

        await connectionFactory.WriteAuditAsync(user.Username, "LOGIN_SUCCESS", "User authenticated.", cancellationToken);
        return user;
    }

    public async Task<HospitalUser> CreateUserAsync(
        string username, string password, string role, CancellationToken cancellationToken = default, string? createdBy = null)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Le nom d'utilisateur est obligatoire.", nameof(username));
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new ArgumentException("Le mot de passe doit contenir au moins 12 caractères.", nameof(password));
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Le rôle est obligatoire.", nameof(role));

        await connectionFactory.InitializeAsync(cancellationToken);
        var user = new HospitalUser
        {
            Id = Guid.NewGuid(),
            Username = username.Trim(),
            PasswordHash = passwordHasher.HashPassword(password),
            Role = role.Trim(),
            IsActive = true
        };
        await users.AddAsync(user, cancellationToken);
        await connectionFactory.WriteAuditAsync(createdBy ?? "system", "USER_CREATED", "Account " + user.Username + " created with role " + user.Role + ".", cancellationToken);
        return user;
    }
}
