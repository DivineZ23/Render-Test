using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Application.Services;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Tests.Services;

public sealed class UserManagementServiceTests
{
    [Fact]
    public async Task Owner_can_assign_and_remove_state_manager_role_with_audit()
    {
        var owner = ActiveOwner("owner");
        var target = ActiveManager("target");
        var audits = new FakeAuditRepository();
        var service = new UserManagementService(new FakeUserRepository([owner, target]), audits);
        var result = await service.ChangeRoleAsync(target.Id, new(UserRole.StateManager, "Observer access"), owner.Id, default);
        Assert.Equal(UserRole.StateManager, result.Role);
        Assert.False(UserRole.StateManager.CanEvict());
        Assert.Contains(audits.Values, x => x.Action == "user.role-changed" && Equals(x.Metadata!["newRole"], "StateManager"));
        result = await service.ChangeRoleAsync(target.Id, new(UserRole.Agent, "Return to agent"), owner.Id, default);
        Assert.Equal(UserRole.Agent, result.Role);
    }

    [Theory]
    [InlineData(UserRole.Manager)]
    [InlineData(UserRole.StateManager)]
    [InlineData(UserRole.Agent)]
    public async Task Only_owner_can_assign_roles(UserRole role)
    {
        var actor = ActiveManager("actor"); actor.Role = role;
        var target = ActiveManager("target");
        var service = new UserManagementService(new FakeUserRepository([actor, target]), new FakeAuditRepository());
        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ChangeRoleAsync(target.Id, new(UserRole.StateManager, "Reason"), actor.Id, default));
        Assert.Equal("OWNER_REQUIRED", error.ErrorCode);
    }

    [Theory]
    [InlineData(UserRole.Owner)]
    [InlineData((UserRole)999)]
    public async Task Role_assignment_cannot_create_owner_or_unknown_roles(UserRole role)
    {
        var owner = ActiveOwner("owner"); var target = ActiveManager("target");
        var service = new UserManagementService(new FakeUserRepository([owner, target]), new FakeAuditRepository());
        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.ChangeRoleAsync(target.Id, new(role, "Reason"), owner.Id, default));
        Assert.Equal("INVALID_USER_ROLE", error.ErrorCode);
    }

    [Fact]
    public async Task Owner_role_assignment_protects_self_other_owners_and_requires_reason()
    {
        var owner = ActiveOwner("owner"); var other = ActiveOwner("other"); var target = ActiveManager("target");
        var service = new UserManagementService(new FakeUserRepository([owner, other, target]), new FakeAuditRepository());
        Assert.Equal("SELF_ROLE_CHANGE_FORBIDDEN", (await Assert.ThrowsAsync<DomainRuleException>(() => service.ChangeRoleAsync(owner.Id, new(UserRole.StateManager, "Reason"), owner.Id, default))).ErrorCode);
        Assert.Equal("OWNER_PROTECTED", (await Assert.ThrowsAsync<DomainRuleException>(() => service.ChangeRoleAsync(other.Id, new(UserRole.StateManager, "Reason"), owner.Id, default))).ErrorCode);
        Assert.Equal("REASON_REQUIRED", (await Assert.ThrowsAsync<DomainRuleException>(() => service.ChangeRoleAsync(target.Id, new(UserRole.StateManager, ""), owner.Id, default))).ErrorCode);
    }

    [Fact]
    public async Task Owner_can_demote_a_manager()
    {
        var manager = ActiveManager("manager-1");
        var owner = ActiveOwner("owner-1");
        var service = new UserManagementService(new FakeUserRepository([manager, owner]), new FakeAuditRepository());
        var result = await service.DemoteAsync(manager.Id, owner.Id, "Role change", default);
        Assert.Equal(UserRole.SeniorAgent, result.Role);
    }

    [Fact]
    public async Task Owner_can_promote_an_active_agent()
    {
        var agent = new User { Id = "agent-1", Role = UserRole.Agent, ApprovalStatus = ApprovalStatus.Approved, AccessStatus = AccessStatus.Active };
        var owner = ActiveOwner("owner-1");
        var service = new UserManagementService(new FakeUserRepository([agent, owner]), new FakeAuditRepository());
        var result = await service.PromoteAsync(agent.Id, owner.Id, default);
        Assert.Equal(UserRole.SeniorAgent, result.Role);
    }

    [Fact]
    public async Task Manager_cannot_promote_an_agent()
    {
        var agent = new User { Id = "agent-1", Role = UserRole.Agent, ApprovalStatus = ApprovalStatus.Approved, AccessStatus = AccessStatus.Active };
        var manager = ActiveManager("manager-1");
        var service = new UserManagementService(new FakeUserRepository([agent, manager]), new FakeAuditRepository());
        var exception = await Assert.ThrowsAsync<DomainRuleException>(() => service.PromoteAsync(agent.Id, manager.Id, default));
        Assert.Equal("OWNER_REQUIRED", exception.ErrorCode);
    }

    [Fact]
    public async Task Owner_account_cannot_be_revoked()
    {
        var owner = ActiveOwner("owner-1");
        var manager = ActiveManager("manager-1");
        var service = new UserManagementService(new FakeUserRepository([owner, manager]), new FakeAuditRepository());
        var exception = await Assert.ThrowsAsync<DomainRuleException>(() => service.RevokeAsync(owner.Id, manager.Id, "No access", default));
        Assert.Equal("OWNER_PROTECTED", exception.ErrorCode);
    }

    [Fact]
    public async Task Approval_activates_pending_agent_and_is_audited()
    {
        var pending = new User { Id = "agent-1", ApprovalStatus = ApprovalStatus.Pending, AccessStatus = AccessStatus.Pending };
        var manager = ActiveManager("manager-1");
        var audits = new FakeAuditRepository();
        var service = new UserManagementService(new FakeUserRepository([pending, manager]), audits);
        var result = await service.ApproveAsync(pending.Id, manager.Id, default);
        Assert.Equal(ApprovalStatus.Approved, result.ApprovalStatus);
        Assert.Equal(AccessStatus.Active, result.AccessStatus);
        Assert.Contains(audits.Values, x => x.Action == "user.approved");
    }

    [Fact]
    public async Task Approved_user_can_update_personal_profile()
    {
        var agent = new User { Id = "agent-1", ApprovalStatus = ApprovalStatus.Approved, AccessStatus = AccessStatus.Active };
        var audits = new FakeAuditRepository();
        var service = new UserManagementService(new FakeUserRepository([agent]), audits);

        var result = await service.UpdateProfileAsync(agent.Id, new UpdateUserProfileRequest("Alex Mercer", 245, "555-0123"), default);

        Assert.Equal("Alex Mercer", result.FullName);
        Assert.Equal(245, result.Cid);
        Assert.Equal("555-0123", result.PhoneNumber);
        Assert.Contains(audits.Values, x => x.Action == "user.profile-updated");
    }

    [Fact]
    public async Task Pending_user_cannot_update_personal_profile()
    {
        var pending = new User { Id = "agent-1", ApprovalStatus = ApprovalStatus.Pending, AccessStatus = AccessStatus.Pending };
        var service = new UserManagementService(new FakeUserRepository([pending]), new FakeAuditRepository());

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.UpdateProfileAsync(pending.Id, new UpdateUserProfileRequest("Alex Mercer", 245, "555-0123"), default));

        Assert.Equal("PROFILE_ACCESS_REQUIRED", exception.ErrorCode);
    }

    [Fact]
    public async Task User_cannot_claim_an_existing_cid()
    {
        var first = new User { Id = "agent-1", Cid = 245, ApprovalStatus = ApprovalStatus.Approved, AccessStatus = AccessStatus.Active };
        var second = new User { Id = "agent-2", ApprovalStatus = ApprovalStatus.Approved, AccessStatus = AccessStatus.Active };
        var service = new UserManagementService(new FakeUserRepository([first, second]), new FakeAuditRepository());

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.UpdateProfileAsync(second.Id, new UpdateUserProfileRequest("Alex Mercer", 245, "555-0123"), default));

        Assert.Equal("USER_CID_EXISTS", exception.ErrorCode);
    }

    private static User ActiveManager(string id) => new() { Id = id, Role = UserRole.Manager, ApprovalStatus = ApprovalStatus.Approved, AccessStatus = AccessStatus.Active };
    private static User ActiveOwner(string id) => new() { Id = id, Role = UserRole.Owner, ApprovalStatus = ApprovalStatus.Approved, AccessStatus = AccessStatus.Active };

    private sealed class FakeUserRepository(IEnumerable<User> seed) : IUserRepository
    {
        private readonly List<User> _users = [.. seed];
        public Task<PagedResult<User>> QueryAsync(int page, int pageSize, ApprovalStatus? approval, AccessStatus? access, UserRole? role, CancellationToken ct) => Task.FromResult(new PagedResult<User>(_users, page, pageSize, _users.Count));
        public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_users.FirstOrDefault(x => x.Id == id));
        public Task<User?> GetByDiscordIdAsync(string id, CancellationToken ct) => Task.FromResult(_users.FirstOrDefault(x => x.DiscordUserId == id));
        public Task<User?> GetByCidAsync(int cid, CancellationToken ct) => Task.FromResult(_users.FirstOrDefault(x => x.Cid == cid));
        public Task<long> CountActiveManagersAsync(CancellationToken ct) => Task.FromResult((long)_users.Count(x => x.Role == UserRole.Manager && x.ApprovalStatus == ApprovalStatus.Approved && x.AccessStatus == AccessStatus.Active && !x.IsDeleted));
        public Task<long> CountPendingAsync(CancellationToken ct) => Task.FromResult((long)_users.Count(x => x.ApprovalStatus == ApprovalStatus.Pending));
        public Task CreateAsync(User user, CancellationToken ct) { _users.Add(user); return Task.CompletedTask; }
        public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAuditRepository : IAuditRepository
    {
        public List<AuditLog> Values { get; } = [];
        public Task CreateAsync(AuditLog value, CancellationToken ct) { Values.Add(value); return Task.CompletedTask; }
        public Task<PagedResult<AuditLog>> QueryAsync(int page, int pageSize, CancellationToken ct) => Task.FromResult(new PagedResult<AuditLog>(Values, page, pageSize, Values.Count));
    }
}
