using System.Security.Claims;
using System.Text.Json;
using ImperialEstates.Api.Authorization;
using ImperialEstates.Api.Middleware;
using ImperialEstates.Application.Common;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ImperialEstates.Tests.Authorization;

public sealed class LiveUserAuthorizationTests
{
    private static User StateManager() => new() { Id = "observer", Role = UserRole.StateManager,
        ApprovalStatus = ApprovalStatus.Approved, AccessStatus = AccessStatus.Active };

    private static DefaultHttpContext Request(string controller, string action, string method = "GET")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Response.Body = new MemoryStream();
        // A former Owner's JWT must not grant write access after their live role changes.
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "observer"), new Claim(ClaimTypes.Role, "Owner")], "test"));
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new ControllerActionDescriptor { ControllerName = controller, ActionName = action }), "test"));
        return context;
    }

    [Theory]
    [InlineData("Properties", "Create", "POST")]
    [InlineData("Properties", "Update", "PUT")]
    [InlineData("Notices", "SetResolution", "PATCH")]
    [InlineData("Properties", "CancelBooking", "DELETE")]
    [InlineData("Users", "ChangeRole", "PUT")]
    [InlineData("AccessManagement", "Update", "PUT")]
    [InlineData("Uploads", "Upload", "POST")]
    [InlineData("Auth", "UpdateProfile", "PUT")]
    [InlineData("Commissions", "CreateSettlement", "POST")]
    public async Task State_manager_cannot_write_even_with_old_owner_claim(string controller, string action, string method)
    {
        var context = Request(controller, action, method);
        var reached = false;
        var middleware = new StateManagerAccessMiddleware(_ => { reached = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(context, new SingleUserRepository(StateManager()), new PermissionRepository());
        Assert.False(reached);
        Assert.Equal(403, context.Response.StatusCode);
    }

    [Fact]
    public async Task State_manager_reads_are_denied_by_default_and_follow_live_page_permissions()
    {
        var settings = new PermissionRepository();
        var repository = new SingleUserRepository(StateManager());
        var reached = 0;
        var middleware = new StateManagerAccessMiddleware(_ => { reached++; return Task.CompletedTask; });
        await middleware.InvokeAsync(Request("Dashboard", "Agents"), repository, settings);
        Assert.Equal(0, reached);
        settings.Permissions["agentAnalytics"] = new() { ["stateManager"] = true };
        var allowed = Request("Dashboard", "Agents");
        await middleware.InvokeAsync(allowed, repository, settings);
        Assert.Equal(1, reached);
        var policy = new AuthorizationHandlerContext([new LiveUserRequirement(true)], allowed.User, allowed);
        await new LiveUserAuthorizationHandler(repository).HandleAsync(policy);
        Assert.True(policy.HasSucceeded);
        settings.Permissions["agentAnalytics"]["stateManager"] = false;
        await middleware.InvokeAsync(Request("Dashboard", "Agents"), repository, settings);
        Assert.Equal(1, reached);
    }

    [Theory]
    [InlineData("GET", false)]
    [InlineData("POST", true)]
    public async Task State_manager_manager_policy_requires_verified_safe_read(string method, bool verified)
    {
        var http = Request("Users", "All", method);
        if (verified) http.Items[StateManagerAccessMiddleware.VerifiedReadAccess] = true;
        var policy = new AuthorizationHandlerContext([new LiveUserRequirement(true)], http.User, http);
        await new LiveUserAuthorizationHandler(new SingleUserRepository(StateManager())).HandleAsync(policy);
        Assert.False(policy.HasSucceeded);
    }

    [Theory]
    [InlineData("Auth", "Me", "GET")]
    [InlineData("Auth", "Logout", "POST")]
    [InlineData("AccessManagement", "Get", "GET")]
    public async Task State_manager_can_bootstrap_and_log_out_without_page_grants(string controller, string action, string method)
    {
        var reached = false;
        var middleware = new StateManagerAccessMiddleware(_ => { reached = true; return Task.CompletedTask; });
        await middleware.InvokeAsync(Request(controller, action, method), new SingleUserRepository(StateManager()), new PermissionRepository());
        Assert.True(reached);
    }

    [Theory]
    [InlineData("overdue", true)]
    [InlineData("evictable", false)]
    [InlineData("paid", false)]
    public async Task Notice_permissions_cannot_be_bypassed_by_changing_status(string status, bool allowed)
    {
        var http = Request("Notices", "Feed");
        http.Request.QueryString = new QueryString("?status=" + status);
        var settings = new PermissionRepository();
        settings.Permissions["notices.overdue"] = new() { ["stateManager"] = true };
        var reached = false;
        await new StateManagerAccessMiddleware(_ => { reached = true; return Task.CompletedTask; })
            .InvokeAsync(http, new SingleUserRepository(StateManager()), settings);
        Assert.Equal(allowed, reached);
    }

    [Fact]
    public async Task Revoked_state_manager_cannot_read_enabled_page()
    {
        var user = StateManager();
        user.AccessStatus = AccessStatus.Revoked;
        var settings = new PermissionRepository();
        settings.Permissions["commissions"] = new() { ["stateManager"] = true };
        var http = Request("Commissions", "Get");
        await new StateManagerAccessMiddleware(_ => throw new Exception("Must not reach endpoint"))
            .InvokeAsync(http, new SingleUserRepository(user), settings);
        Assert.Equal(403, http.Response.StatusCode);
    }

    private sealed class PermissionRepository : ISettingRepository
    {
        public Dictionary<string, Dictionary<string, bool>> Permissions { get; } = [];
        public Task<ApplicationSetting?> GetAsync(string key, CancellationToken ct) => Task.FromResult<ApplicationSetting?>(
            new ApplicationSetting { Key = key, Value = JsonSerializer.Serialize(Permissions) });
        public Task UpsertAsync(ApplicationSetting value, CancellationToken ct) => Task.CompletedTask;
    }

    [Theory]
    [InlineData("Recruitment", "All", "?status=accepted")]
    [InlineData("Notices", "Snapshots", "?summaryOnly=false")]
    [InlineData("FutureController", "All", "")]
    public async Task Unmapped_or_different_page_requests_fail_closed(string controller, string action, string query)
    {
        var http = Request(controller, action);
        http.Request.QueryString = new QueryString(query);
        var settings = new PermissionRepository();
        settings.Permissions["recruitment.pending"] = new() { ["stateManager"] = true };
        settings.Permissions["notices.syncedDataRecords"] = new() { ["stateManager"] = true };
        await new StateManagerAccessMiddleware(_ => throw new Exception("Must not reach endpoint"))
            .InvokeAsync(http, new SingleUserRepository(StateManager()), settings);
        Assert.Equal(403, http.Response.StatusCode);
    }

    [Fact]
    public async Task Active_owner_satisfies_manager_policy()
    {
        var owner = new User
        {
            Id = "owner-id",
            Role = UserRole.Owner,
            ApprovalStatus = ApprovalStatus.Approved,
            AccessStatus = AccessStatus.Active
        };
        var handler = new LiveUserAuthorizationHandler(new SingleUserRepository(owner));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, owner.Id)],
            "test"));
        var requirement = new LiveUserRequirement(true);
        var context = new AuthorizationHandlerContext([requirement], principal, null);

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    private sealed class SingleUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(string id, CancellationToken ct) =>
            Task.FromResult<User?>(id == user.Id ? user : null);

        public Task<User?> GetByDiscordIdAsync(string id, CancellationToken ct) =>
            Task.FromResult<User?>(user.DiscordUserId == id ? user : null);

        public Task<User?> GetByCidAsync(int cid, CancellationToken ct) =>
            Task.FromResult<User?>(user.Cid == cid ? user : null);

        public Task<PagedResult<User>> QueryAsync(int page, int pageSize, ApprovalStatus? approval,
            AccessStatus? access, UserRole? role, CancellationToken ct) =>
            Task.FromResult(new PagedResult<User>([user], page, pageSize, 1));

        public Task<long> CountActiveManagersAsync(CancellationToken ct) => Task.FromResult(1L);
        public Task<long> CountPendingAsync(CancellationToken ct) => Task.FromResult(0L);
        public Task CreateAsync(User value, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateAsync(User value, CancellationToken ct) => Task.CompletedTask;
    }
}
