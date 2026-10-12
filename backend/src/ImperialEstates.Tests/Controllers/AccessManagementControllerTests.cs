using ImperialEstates.Api.Controllers;
using ImperialEstates.Api.Authorization;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;

namespace ImperialEstates.Tests.Controllers;

public sealed class AccessManagementControllerTests
{
    [Fact]
    public void State_manager_defaults_deny_all_and_grants_cannot_enable_actions()
    {
        Assert.All(AccessPermissions.DefaultPermissions().Values, roles => Assert.False(roles["stateManager"]));
        var stored = AccessPermissions.DefaultPermissions();
        foreach (var roles in stored.Values) roles["stateManager"] = true;
        var normalized = AccessPermissions.Normalize(stored);
        Assert.True(normalized["agentAnalytics"]["stateManager"]);
        Assert.True(normalized["administration.users"]["stateManager"]);
        Assert.False(normalized["portfolio.properties.evict"]["stateManager"]);
        Assert.False(normalized["portfolio.properties.book"]["stateManager"]);
        Assert.False(normalized["auction.createListing"]["stateManager"]);
    }

    [Fact]
    public async Task Get_preserves_owner_navigation_choices_but_protects_access_management()
    {
        var setting = new ApplicationSetting
        {
            Key = "access.management",
            Value = """
                {
                  "overview": { "owner": false },
                  "administration.accessManagement": { "owner": false }
                }
                """
        };
        var controller = new AccessManagementController(
            new SettingRepository(setting),
            null!,
            null!);

        var result = await controller.Get(default);

        Assert.False(result.Permissions["overview"]["owner"]);
        Assert.True(result.Permissions["administration.accessManagement"]["owner"]);
    }

    private sealed class SettingRepository(ApplicationSetting setting) : ISettingRepository
    {
        public Task<ApplicationSetting?> GetAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<ApplicationSetting?>(setting);

        public Task UpsertAsync(ApplicationSetting value, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
