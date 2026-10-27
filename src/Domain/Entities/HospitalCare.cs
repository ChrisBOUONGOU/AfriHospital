namespace AfriHospital.src.Domain.Entities;

public sealed record HospitalAdmission(Guid Id, Guid PatientId, DateTime AdmittedAtUtc, string Service, string Reason, DateTime? DischargedAtUtc = null);

public sealed record HospitalBed(Guid Id, string Ward, string RoomNumber, string BedNumber, bool IsActive = true);

public sealed record LaboratoryOrder(Guid Id, Guid PatientId, Guid? ConsultationId, string TestName, string RequestedBy, DateTime RequestedAtUtc, string Status = "Demandée", string? Result = null, DateTime? ResultedAtUtc = null);

public sealed record CountryBillingPolicy(string CountryCode, string CurrencyCode, decimal TaxRatePercent, bool PricesIncludeTax = false)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(CountryCode) || CountryCode.Trim().Length != 2 || CountryCode.Any(c => !char.IsAsciiLetter(c)))
            throw new ArgumentException("Le code pays doit contenir deux lettres ISO.", nameof(CountryCode));
        if (string.IsNullOrWhiteSpace(CurrencyCode) || CurrencyCode.Trim().Length != 3 || CurrencyCode.Any(c => !char.IsAsciiLetter(c)))
            throw new ArgumentException("La devise doit être un code de trois lettres.", nameof(CurrencyCode));
        if (TaxRatePercent < 0m || TaxRatePercent > 100m)
            throw new ArgumentOutOfRangeException(nameof(TaxRatePercent), "Le taux fiscal doit être compris entre 0 et 100 %.");
    }

    public (decimal Net, decimal Tax, decimal Gross) Calculate(decimal amount)
    {
        Validate();
        if (amount < 0m) throw new ArgumentOutOfRangeException(nameof(amount));
        var tax = PricesIncludeTax
            ? decimal.Round(amount - amount / (1m + TaxRatePercent / 100m), 2, MidpointRounding.AwayFromZero)
            : decimal.Round(amount * TaxRatePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var net = PricesIncludeTax ? amount - tax : amount;
        return (net, tax, PricesIncludeTax ? amount : net + tax);
    }
}
