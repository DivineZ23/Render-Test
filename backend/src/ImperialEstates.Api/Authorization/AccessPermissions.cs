using System.Text.Json;
using ImperialEstates.Application.Interfaces;

namespace ImperialEstates.Api.Authorization;

public static class AccessPermissions
{
    public const string SettingKey = "access.management";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static bool CanStateManagerView(string resource) =>
        !resource.StartsWith("portfolio.properties.", StringComparison.Ordinal) &&
        resource != "auction.createListing" && DefaultPermissions().ContainsKey(resource);

    public static async Task<Dictionary<string, Dictionary<string, bool>>> ReadAsync(ISettingRepository settings, CancellationToken ct)
    {
        var setting = await settings.GetAsync(SettingKey, ct);
        if (setting is null) return DefaultPermissions();
        var stored = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, bool>>>(setting.Value, JsonOptions);
        return Normalize(stored ?? []);
    }

    public static Dictionary<string, Dictionary<string, bool>> Normalize(Dictionary<string, Dictionary<string, bool>> values)
    {
        if (!values.ContainsKey("notices.evictionHistory") &&
            values.TryGetValue("notices.evictionList", out var legacyEvictionList))
            values["notices.evictionHistory"] = legacyEvictionList;
        var defaults = DefaultPermissions();
        foreach (var (resource, roles) in defaults)
        {
            if (!values.TryGetValue(resource, out var stored) || stored is null) continue;
            foreach (var role in roles.Keys.ToArray())
            {
                if (role == "stateManager") roles[role] = CanStateManagerView(resource) && stored.GetValueOrDefault(role);
                else if (roles[role] && stored.TryGetValue(role, out var allowed)) roles[role] = allowed;
            }
        }
        defaults["administration.accessManagement"]["owner"] = true;
        return defaults;
    }

    public static Dictionary<string, Dictionary<string, bool>> DefaultPermissions() => new()
    {
        ["overview"] = All(), ["team"] = All(), ["profile"] = All(), ["analytics"] = All(),
        ["agentAnalytics"] = Managers(), ["commissions"] = All(),
        ["auction.createListing"] = All(), ["auction.listings"] = All(),
        ["portfolio.properties"] = All(), ["portfolio.properties.add"] = Managers(),
        ["portfolio.properties.edit"] = Managers(), ["portfolio.properties.sell"] = All(),
        ["portfolio.properties.book"] = All(),
        ["portfolio.properties.evict"] = SeniorAgents(),
        ["portfolio.blocks"] = All(), ["portfolio.tenants"] = All(), ["portfolio.bookings"] = All(),
        ["notices.overdue"] = All(), ["notices.eviction"] = All(), ["notices.overdueList"] = All(),
        ["notices.evictionQueue"] = All(), ["notices.evictionHistory"] = All(), ["notices.syncedDataRecords"] = All(),
        ["notices.sync"] = Managers(),
        ["recruitment.pending"] = Managers(), ["recruitment.accepted"] = Managers(),
        ["recruitment.rejected"] = Managers(), ["administration.users"] = Managers(),
        ["administration.auditLogs"] = Managers(), ["administration.settings"] = Managers(),
        ["administration.accessManagement"] = Owners()
    };

    private static Dictionary<string, bool> All() => new() { ["stateManager"] = false, ["agent"] = true, ["seniorAgent"] = true, ["manager"] = true, ["owner"] = true };
    private static Dictionary<string, bool> Managers() => new() { ["stateManager"] = false, ["agent"] = false, ["seniorAgent"] = false, ["manager"] = true, ["owner"] = true };
    private static Dictionary<string, bool> SeniorAgents() => new() { ["stateManager"] = false, ["agent"] = false, ["seniorAgent"] = true, ["manager"] = true, ["owner"] = true };
    private static Dictionary<string, bool> Owners() => new() { ["stateManager"] = false, ["agent"] = false, ["seniorAgent"] = false, ["manager"] = false, ["owner"] = true };
}
