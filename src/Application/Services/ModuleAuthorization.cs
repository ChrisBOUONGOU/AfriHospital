namespace AfriHospital.src.Application.Services;

/// <summary>Central role-to-module authorization policy. Unknown roles and modules are denied by default.</summary>
public static class ModuleAuthorization
{
    private static readonly IReadOnlyDictionary<string, HashSet<string>> Permissions =
        new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Admin"] = new(StringComparer.OrdinalIgnoreCase)
            {
                "Tableau de bord", "Patients", "Consultations", "Rendez-vous",
                "Pharmacie / Stocks", "Facturation", "Utilisateurs", "Audit", "Sauvegarde"
            },
            ["Doctor"] = new(StringComparer.OrdinalIgnoreCase) { "Tableau de bord", "Patients", "Consultations", "Rendez-vous" },
            ["Nurse"] = new(StringComparer.OrdinalIgnoreCase) { "Tableau de bord", "Patients", "Consultations", "Rendez-vous" },
            ["Receptionist"] = new(StringComparer.OrdinalIgnoreCase) { "Tableau de bord", "Patients", "Rendez-vous" },
            ["Pharmacist"] = new(StringComparer.OrdinalIgnoreCase) { "Tableau de bord", "Pharmacie / Stocks" },
            ["Billing"] = new(StringComparer.OrdinalIgnoreCase) { "Tableau de bord", "Facturation" }
        };

    public static bool CanAccess(string? role, string? module)
    {
        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(module)) return false;
        return Permissions.TryGetValue(role.Trim(), out var modules) && modules.Contains(module.Trim());
    }

    public static IReadOnlyCollection<string> GetAllowedModules(string? role) =>
        !string.IsNullOrWhiteSpace(role) && Permissions.TryGetValue(role.Trim(), out var modules)
            ? modules.ToArray()
            : Array.Empty<string>();
}
