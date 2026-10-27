using Xunit;

namespace AfriHospital.UnitTests;

public sealed class MedicineInventoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AfriHospitalInventoryTests", Guid.NewGuid().ToString("N"));
    private readonly HospitalDatabase _database;

    public MedicineInventoryTests()
    {
        Directory.CreateDirectory(_directory);
        _database = new HospitalDatabase(Path.Combine(_directory, "inventory.db"));
    }

    [Fact]
    public async Task StockMovements_UpdateAggregateAndPreserveBatchHistory()
    {
        await _database.InitializeAsync();
        var medicineId = await _database.AddMedicineAsync("Paracétamol", 0, "boîte", 2.50m, 3);

        await _database.RecordStockMovementAsync(
            medicineId, inbound: true, quantity: 10, batchNumber: "LOT-001",
            expiryDate: DateOnly.FromDateTime(DateTime.Today.AddMonths(6)).ToString("yyyy-MM-dd"),
            supplier: "Fournisseur A", unitCost: 1.20m, selectedBatchId: null, note: "Réception");

        var batches = await _database.GetBatchChoicesAsync(medicineId);
        Assert.Single(batches);
        Assert.Equal(10, batches[0].Quantity);

        await _database.RecordStockMovementAsync(
            medicineId, inbound: false, quantity: 4, batchNumber: null,
            expiryDate: null, supplier: null, unitCost: 0, selectedBatchId: batches[0].Id, note: "Sortie");

        var medicines = await _database.GetMedicineChoicesAsync();
        Assert.Single(medicines);
        Assert.Equal(6, medicines[0].Quantity);

        var movements = await _database.GetStockMovementRowsAsync();
        Assert.Equal(2, movements.Count);
        Assert.Contains(movements, row => row.Contains("IN 10", StringComparison.Ordinal));
        Assert.Contains(movements, row => row.Contains("OUT 4", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StockOut_CannotExceedBatchQuantity_AndFailureDoesNotChangeStock()
    {
        await _database.InitializeAsync();
        var medicineId = await _database.AddMedicineAsync("Gants", 0, "boîte", 4m, 1);
        await _database.RecordStockMovementAsync(
            medicineId, inbound: true, quantity: 3, batchNumber: "G-100",
            expiryDate: DateOnly.FromDateTime(DateTime.Today.AddMonths(3)).ToString("yyyy-MM-dd"),
            supplier: "Fournisseur B", unitCost: 2m, selectedBatchId: null, note: "");

        var batch = Assert.Single(await _database.GetBatchChoicesAsync(medicineId));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _database.RecordStockMovementAsync(
            medicineId, inbound: false, quantity: 4, batchNumber: null,
            expiryDate: null, supplier: null, unitCost: 0, selectedBatchId: batch.Id, note: "Trop"));

        Assert.Equal(3, Assert.Single(await _database.GetMedicineChoicesAsync()).Quantity);
        Assert.Equal(3, Assert.Single(await _database.GetBatchChoicesAsync(medicineId)).Quantity);
        Assert.Single(await _database.GetStockMovementRowsAsync());
    }

    [Fact]
    public async Task StockIn_RejectsPastExpiryDate()
    {
        await _database.InitializeAsync();
        var medicineId = await _database.AddMedicineAsync("Solution saline", 0, "flacon", 1m, 2);

        await Assert.ThrowsAsync<ArgumentException>(() => _database.RecordStockMovementAsync(
            medicineId, inbound: true, quantity: 5, batchNumber: "SAL-OLD",
            expiryDate: DateOnly.FromDateTime(DateTime.Today.AddDays(-1)).ToString("yyyy-MM-dd"),
            supplier: "Fournisseur C", unitCost: 0.50m, selectedBatchId: null, note: ""));

        Assert.Equal(0, Assert.Single(await _database.GetMedicineChoicesAsync()).Quantity);
        Assert.Empty(await _database.GetBatchChoicesAsync(medicineId));
    }

    [Fact]
    public async Task ExistingBatch_CannotBeReusedWithDifferentExpiryOrSupplier()
    {
        await _database.InitializeAsync();
        var medicineId = await _database.AddMedicineAsync("Compresses", 0, "paquet", 3m, 1);
        var expiry = DateOnly.FromDateTime(DateTime.Today.AddMonths(8)).ToString("yyyy-MM-dd");
        await _database.RecordStockMovementAsync(
            medicineId, inbound: true, quantity: 5, batchNumber: "COMP-42",
            expiryDate: expiry, supplier: "Fournisseur D", unitCost: 1m, selectedBatchId: null, note: "");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _database.RecordStockMovementAsync(
            medicineId, inbound: true, quantity: 5, batchNumber: "COMP-42",
            expiryDate: expiry, supplier: "Autre fournisseur", unitCost: 1m, selectedBatchId: null, note: ""));

        Assert.Equal(5, Assert.Single(await _database.GetBatchChoicesAsync(medicineId)).Quantity);
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
