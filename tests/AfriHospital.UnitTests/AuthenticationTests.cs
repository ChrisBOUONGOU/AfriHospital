using AfriHospital.src.Application.Services;
using AfriHospital.src.Infrastructure.Persistence;
using AfriHospital.src.Infrastructure.Repositories;
using AfriHospital.src.Infrastructure.Security;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class AuthenticationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AfriHospitalTests", Guid.NewGuid().ToString("N"));
    private readonly SqliteConnectionFactory _factory;
    private readonly SqliteHospitalUserRepository _users;
    private readonly AuthenticationService _service;

    public AuthenticationTests()
    {
        Directory.CreateDirectory(_directory);
        _factory = new SqliteConnectionFactory(Path.Combine(_directory, "test.db"));
        _users = new SqliteHospitalUserRepository(_factory);
        _service = new AuthenticationService(_factory, _users, new PasswordHasher());
    }

    [Fact]
    public void PasswordHasher_VerifiesCorrectPassword_AndRejectsWrongPassword()
    {
        var hasher = new PasswordHasher();
        var encoded = hasher.HashPassword("Correct-Horse-Battery-42");

        Assert.True(hasher.VerifyPassword("Correct-Horse-Battery-42", encoded));
        Assert.False(hasher.VerifyPassword("wrong-password", encoded));
        Assert.False(hasher.VerifyPassword("anything", "not-a-valid-hash"));
    }

    [Fact]
    public async Task InitialAdministrator_CanBeCreatedOnlyOnce_AndCanAuthenticate()
    {
        var admin = await _service.CreateInitialAdministratorAsync("admin", "Secure-Initial-Password-42");

        Assert.Equal("Admin", admin.Role);
        Assert.Equal(1, await _service.CountUsersAsync());
        Assert.NotNull(await _service.AuthenticateAsync("admin", "Secure-Initial-Password-42"));
        Assert.Null(await _service.AuthenticateAsync("admin", "incorrect-password"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CreateInitialAdministratorAsync("another-admin", "Another-Secure-Password-42"));
    }

    [Fact]
    public async Task UserCreation_RejectsShortPasswords()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateInitialAdministratorAsync("admin", "short"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
