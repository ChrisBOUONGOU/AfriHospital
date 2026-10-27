using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Domain.Interfaces;
using AfriHospital.src.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AfriHospital.src.Infrastructure.Repositories;

/// <summary>Persists administrator-configured billing policies. Rates must be validated by local finance/legal staff.</summary>
public sealed class SqliteCountryBillingPolicyRepository(SqliteConnectionFactory factory) : ICountryBillingPolicyRepository
{
    public async Task SaveAsync(CountryBillingPolicy policy, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        var country = policy.CountryCode.Trim().ToUpperInvariant();
        var currency = policy.CurrencyCode.Trim().ToUpperInvariant();
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO country_billing_policies(country_code,currency_code,tax_rate_percent,prices_include_tax,updated_at_utc)
            VALUES($country,$currency,$rate,$inclusive,$updated)
            ON CONFLICT(country_code) DO UPDATE SET currency_code=excluded.currency_code,
                tax_rate_percent=excluded.tax_rate_percent, prices_include_tax=excluded.prices_include_tax,
                updated_at_utc=excluded.updated_at_utc;
            """;
        command.Parameters.AddWithValue("$country", country);
        command.Parameters.AddWithValue("$currency", currency);
        command.Parameters.AddWithValue("$rate", policy.TaxRatePercent);
        command.Parameters.AddWithValue("$inclusive", policy.PricesIncludeTax ? 1 : 0);
        command.Parameters.AddWithValue("$updated", DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CountryBillingPolicy?> GetByCountryAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) throw new ArgumentException("Le code pays est obligatoire.", nameof(countryCode));
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT country_code,currency_code,tax_rate_percent,prices_include_tax FROM country_billing_policies WHERE country_code=$country;";
        command.Parameters.AddWithValue("$country", countryCode.Trim().ToUpperInvariant());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CountryBillingPolicy(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetInt64(3) == 1)
            : null;
    }

    public async Task<IReadOnlyList<CountryBillingPolicy>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT country_code,currency_code,tax_rate_percent,prices_include_tax FROM country_billing_policies ORDER BY country_code;";
        var policies = new List<CountryBillingPolicy>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            policies.Add(new(reader.GetString(0), reader.GetString(1), reader.GetDecimal(2), reader.GetInt64(3) == 1));
        return policies;
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        await factory.InitializeAsync(cancellationToken);
        var connection = factory.CreateConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS country_billing_policies(
                country_code TEXT PRIMARY KEY NOT NULL CHECK(length(country_code)=2),
                currency_code TEXT NOT NULL CHECK(length(currency_code)=3),
                tax_rate_percent NUMERIC NOT NULL CHECK(tax_rate_percent>=0 AND tax_rate_percent<=100),
                prices_include_tax INTEGER NOT NULL DEFAULT 0 CHECK(prices_include_tax IN (0,1)),
                updated_at_utc TEXT NOT NULL
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }
}
