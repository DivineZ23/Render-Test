using ImperialEstates.Api.Controllers;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;

namespace ImperialEstates.Tests.Controllers;

public sealed class AccessManagementControllerTests
{
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
