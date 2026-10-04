using System.Text.Json;
using ImperialEstates.Api.Extensions;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperialEstates.Api.Controllers;

[ApiController, Route("api/v1/access-management")]
public sealed class AccessManagementController(ISettingRepository settings, IUserRepository users, IAuditRepository audits) : ControllerBase
{
    private const string SettingKey = "access.management";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Authorize(Policy = "ApprovedUser"), HttpGet]
    public async Task<AccessManagementDto> Get(CancellationToken ct) =>
        new(await ReadPermissionsAsync(ct));

    [Authorize(Policy = "Manager"), HttpPut]
    public async Task<AccessManagementDto> Update(AccessManagementDto request, CancellationToken ct)
    {
        var actorId = User.UserId();
        var actor = await users.GetByIdAsync(actorId, ct) ?? throw new UnauthorizedAccessException();
        if (actor.Role != UserRole.Owner) throw new DomainRuleException("Only the Owner can update access management.", "OWNER_REQUIRED");

        var permissions = Normalize(request.Permissions);
        await settings.UpsertAsync(new ApplicationSetting
        {
            Key = SettingKey,
            Value = JsonSerializer.Serialize(permissions, JsonOptions),
            IsPublic = false,
            UpdatedBy = actorId
        }, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = "settings.access_management.updated",
            EntityType = "application_setting",
            EntityId = SettingKey,
            PerformedByUserId = actorId
        }, ct);
        return new AccessManagementDto(permissions);
    }

    private async Task<Dictionary<string, Dictionary<string, bool>>> ReadPermissionsAsync(CancellationToken ct)
    {
        var setting = await settings.GetAsync(SettingKey, ct);
        if (setting is null) return DefaultPermissions();
        var stored = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, bool>>>(setting.Value, JsonOptions);
        return Normalize(stored ?? []);
    }

    private static Dictionary<string, Dictionary<string, bool>> Normalize(Dictionary<string, Dictionary<string, bool>> values)
    {
        if (!values.ContainsKey("notices.evictionHistory") &&
            values.TryGetValue("notices.evictionList", out var legacyEvictionList))
            values["notices.evictionHistory"] = legacyEvictionList;
        var defaults = DefaultPermissions();
        foreach (var (resource, roles) in defaults)
            if (values.TryGetValue(resource, out var stored))
                foreach (var role in roles.Keys.ToArray())
                    if (roles[role] && stored.TryGetValue(role, out var allowed)) roles[role] = allowed;
        defaults["administration.accessManagement"]["owner"] = true;
        return defaults;
    }

    private static Dictionary<string, Dictionary<string, bool>> DefaultPermissions() => new()
    {
        ["overview"] = All(), ["team"] = All(), ["profile"] = All(), ["analytics"] = All(), ["commissions"] = All(),
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

    private static Dictionary<string, bool> All() => new() { ["agent"] = true, ["seniorAgent"] = true, ["manager"] = true, ["owner"] = true };
    private static Dictionary<string, bool> Managers() => new() { ["agent"] = false, ["seniorAgent"] = false, ["manager"] = true, ["owner"] = true };
    private static Dictionary<string, bool> SeniorAgents() => new() { ["agent"] = false, ["seniorAgent"] = true, ["manager"] = true, ["owner"] = true };
    private static Dictionary<string, bool> Owners() => new() { ["agent"] = false, ["seniorAgent"] = false, ["manager"] = false, ["owner"] = true };
}
