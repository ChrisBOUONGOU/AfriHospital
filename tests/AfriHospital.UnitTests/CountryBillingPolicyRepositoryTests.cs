using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Infrastructure.Persistence;
using AfriHospital.src.Infrastructure.Repositories;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class CountryBillingPolicyRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AfriHospitalBillingPolicyTests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;
    public CountryBillingPolicyRepositoryTests() { Directory.CreateDirectory(_directory); _databasePath = Path.Combine(_directory, "billing.db"); }

    [Fact]
    public async Task PolicyCanBeSavedUpdatedAndRetrievedWithoutAssumingLegalRates()
    {
        var repository = new SqliteCountryBillingPolicyRepository(new SqliteConnectionFactory(_databasePath));
        Assert.Null(await repository.GetByCountryAsync("XX"));
        await repository.SaveAsync(new CountryBillingPolicy("xx", "xaf", 0m));
        var first = Assert.IsType<CountryBillingPolicy>(await repository.GetByCountryAsync("XX"));
        Assert.Equal("XX", first.CountryCode);
        Assert.Equal("XAF", first.CurrencyCode);
        Assert.Equal(0m, first.TaxRatePercent);

        await repository.SaveAsync(new CountryBillingPolicy("XX", "XAF", 18m, true));
        var updated = Assert.IsType<CountryBillingPolicy>(await repository.GetByCountryAsync("xx"));
        Assert.Equal(18m, updated.TaxRatePercent);
        Assert.True(updated.PricesIncludeTax);
        Assert.Single(await repository.GetAllAsync());
    }

    [Fact]
    public async Task InvalidPolicyIsRejectedBeforePersistence()
    {
        var repository = new SqliteCountryBillingPolicyRepository(new SqliteConnectionFactory(_databasePath));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.SaveAsync(new CountryBillingPolicy("XX", "XAF", 101m)));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.SaveAsync(new CountryBillingPolicy("X", "XAF", 0m)));
        Assert.Empty(await repository.GetAllAsync());
    }

    public void Dispose() { try { Directory.Delete(_directory, true); } catch { } }
}
