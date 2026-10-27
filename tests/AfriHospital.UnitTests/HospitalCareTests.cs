using AfriHospital.src.Domain.Entities;
using AfriHospital.src.Infrastructure.Persistence;
using AfriHospital.src.Infrastructure.Repositories;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class HospitalCareTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AfriHospitalCareTests", Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;
    public HospitalCareTests() { Directory.CreateDirectory(_directory); _databasePath = Path.Combine(_directory, "care.db"); }

    [Fact]
    public async Task AdmissionOccupiesBedAndDischargeMakesItAvailableAgain()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        await factory.InitializeAsync();
        var patients = new SqlitePatientRepository(factory);
        var patient = new Patient { PatientNumber = "CARE-001", FirstName = "Mariam", LastName = "Diallo", CreatedAtUtc = DateTime.UtcNow };
        await patients.AddAsync(patient);
        var repo = new SqliteHospitalCareRepository(factory);
        var bedId = await repo.AddBedAsync(new HospitalBed(Guid.Empty, "Médecine", "101", "A"));
        Assert.Single(await repo.GetAvailableBedsAsync());
        var admissionId = await repo.AdmitAsync(patient.Id, bedId, DateTime.UtcNow, "Médecine", "Observation");
        Assert.Empty(await repo.GetAvailableBedsAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.AdmitAsync(patient.Id, bedId, DateTime.UtcNow, "Médecine", ""));
        await repo.DischargeAsync(admissionId, DateTime.UtcNow.AddMinutes(1));
        Assert.Single(await repo.GetAvailableBedsAsync());
        Assert.Equal(admissionId, Assert.Single(await repo.GetAdmissionsAsync()).Id);
    }

    [Fact]
    public async Task LaboratoryOrderRequiresExistingPatientAndResultIsRecordedOnce()
    {
        var factory = new SqliteConnectionFactory(_databasePath);
        await factory.InitializeAsync();
        var patients = new SqlitePatientRepository(factory);
        var patient = new Patient { PatientNumber = "LAB-001", FirstName = "Idrissa", LastName = "Ba", CreatedAtUtc = DateTime.UtcNow };
        await patients.AddAsync(patient);
        var repo = new SqliteHospitalCareRepository(factory);
        var id = await repo.RequestLabTestAsync(patient.Id, null, "Numération formule sanguine", "Dr Ba");
        var order = Assert.Single(await repo.GetLabOrdersAsync(patient.Id));
        Assert.Equal("Demandée", order.Status);
        await repo.RecordLabResultAsync(id, "Résultat enregistré pour validation professionnelle");
        order = Assert.Single(await repo.GetLabOrdersAsync(patient.Id));
        Assert.Equal("Résultat disponible", order.Status);
        Assert.NotNull(order.ResultedAtUtc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repo.RecordLabResultAsync(id, "Deuxième résultat"));
    }

    [Fact]
    public void CountryBillingPolicyValidatesInputsAndCalculatesTaxWithoutInventingCountryRates()
    {
        var policy = new CountryBillingPolicy("XX", "XAF", 18m);
        Assert.Equal((100m, 18m, 118m), policy.Calculate(100m));
        var inclusive = new CountryBillingPolicy("XX", "XAF", 18m, true);
        Assert.Equal((100m, 18m, 118m), inclusive.Calculate(118m));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CountryBillingPolicy("XX", "XAF", 101m).Calculate(100m));
        Assert.Throws<ArgumentException>(() => new CountryBillingPolicy("X", "XAF", 0m).Validate());
    }

    public void Dispose() { try { Directory.Delete(_directory, true); } catch { } }
}
