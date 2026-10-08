using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Application.Services;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Tests.Services;

public sealed class DashboardServiceTests
{
    [Fact]
    public async Task Total_revenue_uses_latest_rent_sync_instead_of_stale_property_rent()
    {
        var properties = new PropertyRepository(
            new Property { Id = "property-1", Rent = 1_339_752m },
            new Property { Id = "property-2", Rent = 82_500m });
        var snapshot = new RentSyncSnapshot
        {
            Records =
            [
                new RentSyncRecord { PropertyId = "property-1", Income = 1_339_752m },
                new RentSyncRecord { PropertyId = "property-2", Status = "empty", Income = 0m },
            ],
        };
        var service = CreateService(properties, snapshot);

        var result = await service.GetAsync(default);

        Assert.Equal(1_339_752m, result.TotalRevenue);
        Assert.Equal(1_339_752m, result.TotalProfit);
        Assert.Equal(669_876m, result.AverageProfitPerProperty);
    }

    [Fact]
    public async Task Total_revenue_falls_back_to_property_rent_before_first_sync()
    {
        var service = CreateService(
            new PropertyRepository(
                new Property { Id = "property-1", Rent = 10_000m },
                new Property { Id = "property-2", Rent = 5_000m }),
            snapshot: null);

        var result = await service.GetAsync(default);

        Assert.Equal(15_000m, result.TotalRevenue);
    }

    private static DashboardService CreateService(
        IPropertyRepository properties,
        RentSyncSnapshot? snapshot) =>
        new(
            new BlockRepository(),
            properties,
            new EnquiryRepository(),
            new UserRepository(),
            new StatusHistoryRepository(),
            new RentSyncRepository(snapshot));

    private sealed class BlockRepository : IBlockRepository
    {
        public Task<IReadOnlyList<Block>> GetAllAsync(bool activeOnly, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Block>>([]);
        public Task<Block?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Block?> GetByBusinessIdAsync(int blockId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Block?> GetByNameAsync(string name, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(Block block, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Block block, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class PropertyRepository(params Property[] values) : IPropertyRepository
    {
        public Task<IReadOnlyList<Property>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Property>>(values);
        public Task<PagedResult<Property>> QueryAsync(PropertyQuery query, bool publicOnly, CancellationToken ct) => throw new NotSupportedException();
        public Task<Property?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Property>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Property?> GetByBusinessIdAsync(int propertyId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Property?> GetByNameAsync(string propertyName, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Property>> GetFeaturedAsync(int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountByBlockAsync(string blockId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountByStatusAsync(PropertyStatus? status, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(Property property, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Property property, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class EnquiryRepository : IEnquiryRepository
    {
        public Task<long> CountPendingAsync(CancellationToken ct) => Task.FromResult(0L);
        public Task<PagedResult<Enquiry>> QueryAsync(int page, int pageSize, EnquiryStatus? status, CancellationToken ct) => throw new NotSupportedException();
        public Task<Enquiry?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(Enquiry enquiry, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Enquiry enquiry, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class UserRepository : IUserRepository
    {
        public Task<long> CountPendingAsync(CancellationToken ct) => Task.FromResult(0L);
        public Task<PagedResult<User>> QueryAsync(int page, int pageSize, ApprovalStatus? approval, AccessStatus? access, UserRole? role, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByDiscordIdAsync(string discordId, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByCidAsync(int cid, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountActiveManagersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(User user, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class StatusHistoryRepository : IStatusHistoryRepository
    {
        public Task<IReadOnlyList<PropertyStatusHistory>> GetRecentAsync(int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PropertyStatusHistory>>([]);
        public Task CreateAsync(PropertyStatusHistory history, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PropertyStatusHistory>> GetByPropertyAsync(string propertyId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RentSyncRepository(RentSyncSnapshot? snapshot) : IRentSyncRepository
    {
        public Task<RentSyncSnapshot?> GetCurrentAsync(CancellationToken ct) => Task.FromResult(snapshot);
        public Task<RentSyncSnapshot?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<RentSyncSnapshot>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task SaveCurrentAsync(RentSyncSnapshot value, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(RentSyncSnapshot value, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string id, CancellationToken ct) => throw new NotSupportedException();
    }
}
