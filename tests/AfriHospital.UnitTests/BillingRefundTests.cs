using AfriHospital;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class BillingRefundTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "AfriHospitalRefundTests", Guid.NewGuid().ToString("N"));
    private readonly HospitalDatabase _database;

    public BillingRefundTests()
    {
        Directory.CreateDirectory(_directory);
        _database = new HospitalDatabase(Path.Combine(_directory, "billing.db"));
    }

    [Fact]
    public async Task PartialRefund_UpdatesNetBalanceAndInvoiceStatus()
    {
        await _database.InitializeAsync();
        await _database.ExecuteAsync(
            "INSERT INTO invoices(id,patient_name,description,amount,currency,status) VALUES(1,'Patient test','Consultation',100,'XAF','À payer')");
        const long invoiceId = 1;
        var paymentId = await _database.RecordPaymentAsync(invoiceId, 100m, "Espèces");

        await _database.RecordRefundAsync(paymentId, 25m, "Mobile money", "Correction de paiement");

        var outstanding = Assert.Single(await _database.GetInvoiceChoicesAsync());
        Assert.Equal(75m, outstanding.Paid);
        Assert.Equal(100m, outstanding.Total);
        Assert.Equal(75m, outstanding.Total - outstanding.Paid);
        Assert.Contains("Partiellement payée", Assert.Single(await _database.GetRowsAsync(
            "SELECT status FROM invoices WHERE id=$id", r => r.GetString(0), ("$id", invoiceId))));

        var refundable = Assert.Single(await _database.GetRefundablePaymentsAsync());
        Assert.Equal(75m, refundable.Available);

        await _database.RecordPaymentAsync(invoiceId, 25m, "Virement");
        Assert.Empty(await _database.GetInvoiceChoicesAsync());
        Assert.Contains("Payée", Assert.Single(await _database.GetRowsAsync(
            "SELECT status FROM invoices WHERE id=$id", r => r.GetString(0), ("$id", invoiceId))));
    }

    [Fact]
    public async Task Refund_CannotExceedRemainingRefundablePaymentAmount()
    {
        await _database.InitializeAsync();
        await _database.ExecuteAsync(
            "INSERT INTO invoices(id,patient_name,description,amount,currency,status) VALUES(1,'Patient test','Acte',50,'XAF','À payer')");
        var invoiceId = Convert.ToInt64(await ScalarAsync("SELECT last_insert_rowid();"));
        var paymentId = await _database.RecordPaymentAsync(invoiceId, 50m, "Carte");
        await _database.RecordRefundAsync(paymentId, 20m, "Espèces", "Remboursement partiel");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _database.RecordRefundAsync(paymentId, 31m, "Espèces", "Montant excessif"));

        var refundable = Assert.Single(await _database.GetRefundablePaymentsAsync());
        Assert.Equal(30m, refundable.Available);
        var net = Assert.Single(await _database.GetInvoiceChoicesAsync());
        Assert.Equal(30m, net.Paid);
    }

    [Fact]
    public async Task FullRefund_MakesInvoicePayableAgain()
    {
        await _database.InitializeAsync();
        await _database.ExecuteAsync(
            "INSERT INTO invoices(id,patient_name,description,amount,currency,status) VALUES(1,'Patient test','Analyse',40,'XAF','À payer')");
        var invoiceId = Convert.ToInt64(await ScalarAsync("SELECT last_insert_rowid();"));
        var paymentId = await _database.RecordPaymentAsync(invoiceId, 40m, "Virement");
        await _database.RecordRefundAsync(paymentId, 40m, "Mobile money", "Annulation de prestation");

        var invoice = Assert.Single(await _database.GetInvoiceChoicesAsync());
        Assert.Equal(0m, invoice.Paid);
        Assert.Contains("À payer", Assert.Single(await _database.GetRowsAsync(
            "SELECT status FROM invoices WHERE id=$id", r => r.GetString(0), ("$id", invoiceId))));
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
