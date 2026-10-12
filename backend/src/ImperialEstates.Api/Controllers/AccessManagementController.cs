using System.Text.Json;
using ImperialEstates.Api.Authorization;
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
    private const string SettingKey = AccessPermissions.SettingKey;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Authorize(Policy = "ApprovedUser"), HttpGet]
    public async Task<AccessManagementDto> Get(CancellationToken ct) =>
        new(await AccessPermissions.ReadAsync(settings, ct));

    [Authorize(Policy = "Manager"), HttpPut]
    public async Task<AccessManagementDto> Update(AccessManagementDto request, CancellationToken ct)
    {
        var actorId = User.UserId();
        var actor = await users.GetByIdAsync(actorId, ct) ?? throw new UnauthorizedAccessException();
        if (actor.Role != UserRole.Owner) throw new DomainRuleException("Only the Owner can update access management.", "OWNER_REQUIRED");

        var permissions = AccessPermissions.Normalize(request.Permissions);
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

}
