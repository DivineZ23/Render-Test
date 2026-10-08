using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Application.Services;

public sealed class AuthService(IUserRepository users, ITokenService tokens, IOwnerIdentity ownerIdentity)
{
    public async Task<AuthResult> SignInAsync(DiscordProfile profile, CancellationToken cancellationToken)
    {
        var user = await users.GetByDiscordIdAsync(profile.Id, cancellationToken);
        if (user is null)
        {
            user = new User { DiscordUserId = profile.Id, Username = profile.Username, DisplayName = profile.DisplayName, AvatarUrl = profile.AvatarUrl, Email = profile.Email };
            if (ownerIdentity.IsOwner(profile.Id)) ActivateOwner(user);
            await users.CreateAsync(user, cancellationToken);
        }
        else
        {
            user.Username = profile.Username; user.DisplayName = profile.DisplayName; user.AvatarUrl = profile.AvatarUrl;
            user.Email = profile.Email ?? user.Email; user.LastLoginAt = DateTime.UtcNow; user.UpdatedAt = DateTime.UtcNow;
            if (ownerIdentity.IsOwner(profile.Id)) ActivateOwner(user);
            await users.UpdateAsync(user, cancellationToken);
        }
        var (token, expiresAt) = tokens.Create(user);
        return new(user.ToDto(), token, expiresAt);
    }

    private static void ActivateOwner(User user)
    {
        user.Role = UserRole.Owner;
        user.ApprovalStatus = ApprovalStatus.Approved;
        user.AccessStatus = AccessStatus.Active;
        user.RevokedAt = null;
        user.RevokedBy = null;
        user.RevokeReason = null;
    }
}

public sealed class DashboardService(
    IBlockRepository blocks,
    IPropertyRepository properties,
    IEnquiryRepository enquiries,
    IUserRepository users,
    IStatusHistoryRepository history,
    IRentSyncRepository rentSync)
{
    public async Task<DashboardSummaryDto> GetAsync(CancellationToken ct)
    {
        var blocksTask = blocks.GetAllAsync(true, ct);
        var propertiesTask = properties.GetAllAsync(ct);
        var pendingEnquiriesTask = enquiries.CountPendingAsync(ct);
        var pendingUsersTask = users.CountPendingAsync(ct);
        var recentHistoryTask = history.GetRecentAsync(8, ct);
        var latestRentSyncTask = rentSync.GetCurrentAsync(ct);
        await Task.WhenAll(
            blocksTask,
            propertiesTask,
            pendingEnquiriesTask,
            pendingUsersTask,
            recentHistoryTask,
            latestRentSyncTask);
        var activeBlocks = blocksTask.Result;
        var allProperties = propertiesTask.Result;
        var totalCost = allProperties.Sum(property => property.Type.StateCost() ?? 0);
        var latestRentSync = latestRentSyncTask.Result;
        var totalRevenue = latestRentSync is null
            ? allProperties.Sum(property => property.Rent)
            : latestRentSync.Records.Sum(record => record.Income);
        var totalProfit = totalRevenue - totalCost;
        var blockNames = activeBlocks.ToDictionary(block => block.Id, block => block.BlockName);
        var blockFinancials = allProperties
            .Where(property => blockNames.ContainsKey(property.BlockId))
            .GroupBy(property => property.BlockId)
            .Select(group => new
            {
                Name = blockNames[group.Key],
                Profit = group.Sum(property => property.Rent) - group.Sum(property => property.Type.StateCost() ?? 0)
            })
            .OrderByDescending(block => block.Profit)
            .FirstOrDefault();

        return new(
            activeBlocks.Count,
            allProperties.Count,
            allProperties.Count(property => property.Status == PropertyStatus.Available),
            allProperties.Count(property => property.Status == PropertyStatus.Booked),
            allProperties.Count(property => property.CurrentTenantId is not null),
            totalRevenue,
            totalCost,
            totalProfit,
            allProperties.Count == 0 ? 0 : totalProfit / allProperties.Count,
            blockFinancials?.Name,
            blockFinancials?.Profit ?? 0,
            pendingEnquiriesTask.Result,
            pendingUsersTask.Result,
            recentHistoryTask.Result.Select(x => x.ToDto()).ToList());
    }
}
