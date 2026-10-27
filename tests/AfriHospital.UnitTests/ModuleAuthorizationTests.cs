using AfriHospital.src.Application.Services;
using Xunit;

namespace AfriHospital.UnitTests;

public sealed class ModuleAuthorizationTests
{
    [Theory]
    [InlineData("Admin", "Utilisateurs", true)]
    [InlineData("Admin", "Sauvegarde", true)]
    [InlineData("Doctor", "Consultations", true)]
    [InlineData("Doctor", "Facturation", false)]
    [InlineData("Receptionist", "Patients", true)]
    [InlineData("Receptionist", "Consultations", false)]
    [InlineData("Pharmacist", "Pharmacie / Stocks", true)]
    [InlineData("Pharmacist", "Patients", false)]
    [InlineData("Billing", "Facturation", true)]
    [InlineData("Billing", "Audit", false)]
    [InlineData("unknown", "Audit", false)]
    public void CanAccess_UsesLeastPrivilegeRoleMatrix(string role, string module, bool expected) =>
        Assert.Equal(expected, ModuleAuthorization.CanAccess(role, module));

    [Fact]
    public void CanAccess_IsCaseInsensitiveAndRejectsMissingValues()
    {
        Assert.True(ModuleAuthorization.CanAccess(" doctor ", "consultations"));
        Assert.False(ModuleAuthorization.CanAccess(null, "Patients"));
        Assert.False(ModuleAuthorization.CanAccess("Admin", null));
        Assert.False(ModuleAuthorization.CanAccess("Admin", "Unknown module"));
    }

    [Fact]
    public void UnknownRoleGetsNoModules()
    {
        Assert.Empty(ModuleAuthorization.GetAllowedModules("custom-role"));
        Assert.Empty(ModuleAuthorization.GetAllowedModules(null));
    }
}
