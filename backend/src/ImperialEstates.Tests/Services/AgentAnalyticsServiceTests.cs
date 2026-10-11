using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Application.Services;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Tests.Services;

public sealed class AgentAnalyticsServiceTests
{
    [Fact]
    public async Task Aggregates_agent_activity_and_respects_the_selected_period()
    {
        var now = DateTime.UtcNow;
        var alpha = Agent("alpha", "Alpha Agent", UserRole.Agent);
        var beta = Agent("beta", "Beta Senior", UserRole.SeniorAgent);
        var service = new AgentAnalyticsService(
            new UserRepository(alpha, beta),
            new BookingRepository(
                Booking("alpha", 100m, BookingStatus.Converted, now.AddDays(-2)),
                Booking("alpha", 50m, BookingStatus.Active, now.AddDays(-1)),
                Booking("beta", 200m, BookingStatus.Active, now.AddDays(-3)),
                Booking("alpha", 9_999m, BookingStatus.Active, now.AddDays(-90))),
            new TenantRepository(
                new Tenant { Id = "sale", CreatedBy = "alpha", CreatedAt = now.AddDays(-2) },
                new Tenant { Id = "eviction", EvictedByUserId = "beta", EndDate = now.AddDays(-1) }),
            new CommissionRepository(
                Commission("alpha", 300m, true, now.AddDays(-1)),
                Commission("alpha", 100m, false, now.AddDays(-2)),
                Commission("beta", 50m, false, now.AddDays(-2))),
            new RentSyncRepository(new RentSyncSnapshot
            {
                Records =
                [
                    Notice("alpha", now.AddDays(-1)),
                    Notice("beta", now.AddDays(-1)),
                ],
            }));

        var result = await service.GetAsync(now.AddDays(-30), now, default);

        Assert.Equal(2, result.Totals.Agents);
        Assert.Equal(3, result.Totals.Bookings);
        Assert.Equal(350m, result.Totals.BookingAmount);
        Assert.Equal(1, result.Totals.HousesSold);
        Assert.Equal(300m, result.Totals.CommissionTaken);
        Assert.Equal(150m, result.Totals.CommissionOwed);
        Assert.Equal(1, result.Totals.HousesEvicted);
        Assert.Equal(2, result.Totals.NoticesSent);

        var alphaResult = Assert.Single(result.Agents, value => value.UserId == "alpha");
        Assert.Equal(2, alphaResult.Bookings);
        Assert.Equal(150m, alphaResult.BookingAmount);
        Assert.Equal(1, alphaResult.ConvertedBookings);
        Assert.Equal(50m, alphaResult.ConversionRate);
        Assert.Equal(1, alphaResult.HousesSold);
        Assert.Equal(300m, alphaResult.CommissionTaken);
        Assert.Equal(100m, alphaResult.CommissionOwed);
        Assert.Equal(1, alphaResult.NoticesSent);
        Assert.True(alphaResult.PerformanceScore > 0);
    }

    private static User Agent(string id, string name, UserRole role) => new()
    {
        Id = id,
        DisplayName = name,
        FullName = name,
        Role = role,
        ApprovalStatus = ApprovalStatus.Approved,
        AccessStatus = AccessStatus.Active,
    };

    private static PropertyBooking Booking(
        string userId, decimal amount, BookingStatus status, DateTime createdAt) => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedBy = userId,
            CreatedAt = createdAt,
            BookingAmount = amount,
            Status = status,
        };

    private static CommissionRecord Commission(
        string userId, decimal amount, bool isPaid, DateTime occurredAt) => new()
        {
            AgentUserId = userId,
            CommissionAmount = amount,
            IsPaid = isPaid,
            PaidAt = isPaid ? occurredAt : null,
            CreatedAt = occurredAt,
        };

    private static RentSyncRecord Notice(string userId, DateTime occurredAt) => new()
    {
        Status = "overdue",
        IsResolved = true,
        ResolvedByUserId = userId,
        ResolvedAt = occurredAt,
    };

    private sealed class UserRepository(params User[] values) : IUserRepository
    {
        public Task<PagedResult<User>> QueryAsync(
            int page, int pageSize, ApprovalStatus? approval, AccessStatus? access,
            UserRole? role, CancellationToken ct)
        {
            var matches = values.Where(value =>
                (!approval.HasValue || value.ApprovalStatus == approval) &&
                (!access.HasValue || value.AccessStatus == access) &&
                (!role.HasValue || value.Role == role)).ToList();
            return Task.FromResult(new PagedResult<User>(matches, page, pageSize, matches.Count));
        }
        public Task<User?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByDiscordIdAsync(string discordId, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByCidAsync(int cid, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountActiveManagersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountPendingAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(User user, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class BookingRepository(params PropertyBooking[] values) : IPropertyBookingRepository
    {
        public Task<IReadOnlyList<PropertyBooking>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PropertyBooking>>(values);
        public Task<IReadOnlyList<PropertyBooking>> GetAllActiveAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PropertyBooking>> GetActiveByPropertyAsync(string propertyId, CancellationToken ct) => throw new NotSupportedException();
        public Task<PropertyBooking?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountActiveByPropertyAsync(string propertyId, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(PropertyBooking booking, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(PropertyBooking booking, CancellationToken ct) => throw new NotSupportedException();
        public Task CloseActiveAsync(string propertyId, BookingStatus status, string actorId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class TenantRepository(params Tenant[] values) : ITenantRepository
    {
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Tenant>>(values);
        public Task<PagedResult<Tenant>> QueryAsync(int page, int pageSize, CancellationToken ct) => throw new NotSupportedException();
        public Task<Tenant?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetByCidsAsync(IReadOnlyCollection<int> cids, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Tenant>> GetEvictedAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(Tenant tenant, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Tenant tenant, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CommissionRepository(params CommissionRecord[] values) : ICommissionRepository
    {
        public Task<IReadOnlyList<CommissionRecord>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CommissionRecord>>(values);
        public Task CreateManyAsync(IReadOnlyCollection<CommissionRecord> records, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommissionRecord>> GetByAgentAsync(string userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<CommissionRecord>> GetBySettlementIdAsync(string settlementId, CancellationToken ct) => throw new NotSupportedException();
        public Task<CommissionRecord?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(CommissionRecord commission, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RentSyncRepository(params RentSyncSnapshot[] values) : IRentSyncRepository
    {
        public Task<IReadOnlyList<RentSyncSnapshot>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<RentSyncSnapshot>>(values);
        public Task<RentSyncSnapshot?> GetCurrentAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<RentSyncSnapshot?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task SaveCurrentAsync(RentSyncSnapshot snapshot, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(RentSyncSnapshot snapshot, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(string id, CancellationToken ct) => throw new NotSupportedException();
    }
}
