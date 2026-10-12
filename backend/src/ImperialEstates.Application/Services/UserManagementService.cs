using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Application.Services;

public sealed class UserManagementService(IUserRepository users, IAuditRepository audits)
{
    public async Task<PagedResult<UserDto>> QueryAsync(int page, int pageSize, ApprovalStatus? approval, AccessStatus? access, UserRole? role, CancellationToken cancellationToken)
    {
        var values = await users.QueryAsync(page, pageSize, approval, access, role, cancellationToken);
        return new(values.Items.Select(x => x.ToDto()).ToList(), values.Page, values.PageSize, values.TotalItems);
    }

    public async Task<UserDto> GetAsync(string id, CancellationToken cancellationToken) => (await GetEntityAsync(id, cancellationToken)).ToDto();

    public async Task<UserDto> UpdateProfileAsync(string id, UpdateUserProfileRequest request, CancellationToken ct)
    {
        var user = await GetEntityAsync(id, ct);
        if (user.Role == UserRole.StateManager) throw new UnauthorizedAccessException();
        if (user.ApprovalStatus != ApprovalStatus.Approved || user.AccessStatus != AccessStatus.Active)
            throw new DomainRuleException("Your account must be approved and active before editing personal details.", "PROFILE_ACCESS_REQUIRED");

        var existingCid = await users.GetByCidAsync(request.Cid, ct);
        if (existingCid is not null && existingCid.Id != user.Id)
            throw new DomainRuleException("That CID is already assigned to another team member.", "USER_CID_EXISTS");

        return await MutateAsync(user, id, "user.profile-updated", value =>
        {
            value.FullName = request.FullName.Trim();
            value.Cid = request.Cid;
            value.PhoneNumber = request.PhoneNumber.Trim();
        }, null, ct);
    }

    public async Task<UserDto> ApproveAsync(string id, string actorId, CancellationToken ct)
    {
        var actor = await GetEntityAsync(actorId, ct);
        var target = await GetEntityAsync(id, ct);
        EnsureCanManageAccess(actor, target);
        return await MutateAsync(target, actorId, "user.approved", user =>
        {
            user.ApprovalStatus = ApprovalStatus.Approved; user.AccessStatus = AccessStatus.Active;
            user.ApprovedAt = DateTime.UtcNow; user.ApprovedBy = actorId; user.RevokeReason = null;
        }, null, ct);
    }

    public async Task<UserDto> RejectAsync(string id, string actorId, string? reason, CancellationToken ct)
    {
        RequireReason(reason);
        var actor = await GetEntityAsync(actorId, ct);
        var target = await GetEntityAsync(id, ct);
        EnsureCanManageAccess(actor, target);
        return await MutateAsync(target, actorId, "user.rejected", user =>
        {
            user.ApprovalStatus = ApprovalStatus.Rejected; user.AccessStatus = AccessStatus.Revoked; user.RevokeReason = reason!.Trim();
        }, reason, ct);
    }

    public async Task<UserDto> ChangeRoleAsync(string id, ChangeUserRoleRequest request, string actorId, CancellationToken ct)
    {
        var actor = await GetEntityAsync(actorId, ct);
        RequireOwner(actor);
        if (id == actorId) throw new DomainRuleException("You cannot change your own role.", "SELF_ROLE_CHANGE_FORBIDDEN");
        var target = await GetEntityAsync(id, ct);
        ProtectOwner(target);
        if (target.IsDeleted) throw new KeyNotFoundException("User not found.");
        if (request.Role is not (UserRole.Agent or UserRole.SeniorAgent or UserRole.Manager or UserRole.StateManager))
            throw new DomainRuleException("Choose Agent, Senior Agent, Manager or State Manager.", "INVALID_USER_ROLE");
        RequireReason(request.Reason);
        var previous = target.Role;
        target.Role = request.Role;
        target.UpdatedAt = DateTime.UtcNow;
        target.UpdatedBy = actorId;
        await users.UpdateAsync(target, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = "user.role-changed", EntityType = "user", EntityId = id, PerformedByUserId = actorId,
            Metadata = new() { ["previousRole"] = previous.ToString(), ["newRole"] = request.Role.ToString(), ["reason"] = request.Reason!.Trim() }
        }, ct);
        return target.ToDto();
    }

    public async Task<UserDto> PromoteAsync(string id, string actorId, CancellationToken ct)
    {
        var actor = await GetEntityAsync(actorId, ct);
        RequireOwner(actor);
        if (id == actorId) throw new DomainRuleException("You cannot promote yourself.", "SELF_ROLE_CHANGE_FORBIDDEN");
        var target = await GetEntityAsync(id, ct);
        if (target.Role is not (UserRole.Agent or UserRole.SeniorAgent) || target.ApprovalStatus != ApprovalStatus.Approved || target.AccessStatus != AccessStatus.Active)
            throw new DomainRuleException("Only an approved active agent or senior agent can be promoted.", "USER_NOT_ACTIVE");
        var nextRole = target.Role == UserRole.Agent ? UserRole.SeniorAgent : UserRole.Manager;
        return await MutateAsync(target, actorId, "user.promoted", user => user.Role = nextRole, null, ct);
    }

    public async Task<UserDto> DemoteAsync(string id, string actorId, string? reason, CancellationToken ct)
    {
        RequireReason(reason);
        if (id == actorId) throw new DomainRuleException("You cannot demote yourself.", "SELF_ROLE_CHANGE_FORBIDDEN");
        var actor = await GetEntityAsync(actorId, ct);
        RequireOwner(actor);
        var target = await GetEntityAsync(id, ct);
        ProtectOwner(target);
        if (target.Role is not (UserRole.Manager or UserRole.SeniorAgent))
            throw new DomainRuleException("Only a manager or senior agent can be demoted.", "USER_NOT_MANAGER");
        var nextRole = target.Role == UserRole.Manager ? UserRole.SeniorAgent : UserRole.Agent;
        return await MutateAsync(target, actorId, "user.demoted", user => user.Role = nextRole, reason, ct);
    }

    public async Task<UserDto> RevokeAsync(string id, string actorId, string? reason, CancellationToken ct)
    {
        RequireReason(reason);
        if (id == actorId) throw new DomainRuleException("You cannot revoke your own access.", "SELF_ACCESS_CHANGE_FORBIDDEN");
        var actor = await GetEntityAsync(actorId, ct);
        var target = await GetEntityAsync(id, ct);
        EnsureCanManageAccess(actor, target);
        return await MutateAsync(target, actorId, "user.revoked", user =>
        {
            user.AccessStatus = AccessStatus.Revoked; user.RevokedAt = DateTime.UtcNow; user.RevokedBy = actorId; user.RevokeReason = reason!.Trim();
        }, reason, ct);
    }

    public async Task<UserDto> RestoreAsync(string id, string actorId, CancellationToken ct)
    {
        var actor = await GetEntityAsync(actorId, ct);
        var target = await GetEntityAsync(id, ct);
        EnsureCanManageAccess(actor, target);
        if (target.ApprovalStatus != ApprovalStatus.Approved) throw new DomainRuleException("Only approved users can be restored.", "USER_NOT_APPROVED");
        return await MutateAsync(target, actorId, "user.restored", user =>
        {
            user.AccessStatus = AccessStatus.Active; user.RevokedAt = null; user.RevokedBy = null; user.RevokeReason = null;
        }, null, ct);
    }

    public async Task DeleteAsync(string id, string actorId, string? reason, CancellationToken ct)
    {
        RequireReason(reason);
        if (id == actorId) throw new DomainRuleException("You cannot delete your own account.", "SELF_DELETE_FORBIDDEN");
        var actor = await GetEntityAsync(actorId, ct);
        var target = await GetEntityAsync(id, ct);
        EnsureCanManageAccess(actor, target);
        target.IsDeleted = true; target.AccessStatus = AccessStatus.Revoked; target.UpdatedBy = actorId; target.UpdatedAt = DateTime.UtcNow;
        await users.UpdateAsync(target, ct);
        await LogAsync("user.deleted", target.Id, actorId, reason, ct);
    }

    private async Task<UserDto> MutateAsync(string id, string actorId, string action, Action<User> mutation, CancellationToken ct) =>
        await MutateAsync(await GetEntityAsync(id, ct), actorId, action, mutation, null, ct);

    private async Task<UserDto> MutateAsync(User value, string actorId, string action, Action<User> mutation, string? reason, CancellationToken ct)
    {
        mutation(value); value.UpdatedAt = DateTime.UtcNow; value.UpdatedBy = actorId;
        await users.UpdateAsync(value, ct); await LogAsync(action, value.Id, actorId, reason, ct); return value.ToDto();
    }

    private async Task LogAsync(string action, string entityId, string actorId, string? reason, CancellationToken ct) =>
        await audits.CreateAsync(new AuditLog { Action = action, EntityType = "user", EntityId = entityId, PerformedByUserId = actorId, Metadata = new() { ["reason"] = reason } }, ct);
    private async Task<User> GetEntityAsync(string id, CancellationToken ct) => await users.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("User not found.");
    private static void RequireOwner(User actor)
    {
        if (actor.Role != UserRole.Owner || actor.IsDeleted || actor.ApprovalStatus != ApprovalStatus.Approved || actor.AccessStatus != AccessStatus.Active)
            throw new DomainRuleException("Only an active Owner can change user roles.", "OWNER_REQUIRED");
    }
    private static void ProtectOwner(User target)
    {
        if (target.Role == UserRole.Owner)
            throw new DomainRuleException("The Owner account is protected.", "OWNER_PROTECTED");
    }
    private static void EnsureCanManageAccess(User actor, User target)
    {
        if (actor.Role is not (UserRole.Manager or UserRole.Owner)) throw new UnauthorizedAccessException();
        ProtectOwner(target);
        if (target.Role is (UserRole.Manager or UserRole.StateManager) && actor.Role != UserRole.Owner)
            throw new DomainRuleException("Only the Owner can change a Manager or State Manager's access.", "OWNER_REQUIRED");
    }
    private static void RequireReason(string? reason) { if (string.IsNullOrWhiteSpace(reason)) throw new DomainRuleException("A reason is required.", "REASON_REQUIRED"); }
}
