using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Application.Services;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Tests.Services;

public sealed class CommissionServiceTests
{
    [Fact]
    public void Base_price_only_goes_entirely_to_winner()
    {
        var result = CommissionService.Calculate(10_000, 10_000, 3);

        Assert.Equal(10_000, result.WinningAgentTotal);
        Assert.Equal(0, result.AdditionalAgentPool);
        Assert.Equal(0, result.AmountPerOtherAgent);
    }

    [Fact]
    public void Premium_slabs_are_applied_and_split_sixty_forty()
    {
        var result = CommissionService.Calculate(410_000, 10_000, 3);

        Assert.Equal(400_000, result.AuctionPremium);
        Assert.Equal(80_000, result.AdditionalAgentPool);
        Assert.Equal(48_000, result.WinningAgentClosingShare);
        Assert.Equal(58_000, result.WinningAgentTotal);
        Assert.Equal(32_000, result.ParticipationPool);
        Assert.Equal(16_000, result.AmountPerOtherAgent);
    }

    [Theory]
    [InlineData(50_000, 15_000)]
    [InlineData(100_000, 30_000)]
    [InlineData(250_000, 60_000)]
    [InlineData(500_000, 90_000)]
    [InlineData(1_000_000, 140_000)]
    [InlineData(2_000_000, 240_000)]
    [InlineData(2_100_000, 250_000)]
    [InlineData(3_000_000, 250_000)]
    public void Agent_pool_uses_the_agreed_marginal_rates_and_cap(int premium, int expectedPool)
    {
        var result = CommissionService.Calculate(premium, 0, 2);

        Assert.Equal(expectedPool, result.AdditionalAgentPool);
    }

    [Fact]
    public void Additional_pool_is_capped_at_two_hundred_fifty_thousand()
    {
        var result = CommissionService.Calculate(3_000_000, 0, 2);

        Assert.Equal(250_000, result.AdditionalAgentPool);
        Assert.Equal(150_000, result.WinningAgentClosingShare);
        Assert.Equal(100_000, result.AmountPerOtherAgent);
    }

    [Fact]
    public void No_other_agents_means_no_participation_payout()
    {
        var result = CommissionService.Calculate(110_000, 10_000, 1);

        Assert.Equal(30_000, result.AdditionalAgentPool);
        Assert.Equal(18_000, result.WinningAgentClosingShare);
        Assert.Equal(0, result.ParticipationPool);
    }

    [Fact]
    public void Multiple_winners_split_the_base_and_winner_pool_equally()
    {
        var result = CommissionService.Calculate(510_000, 10_000, 5, 2);

        Assert.Equal(90_000, result.AdditionalAgentPool);
        Assert.Equal(410_000, result.CompanyShare);
        Assert.Equal(2, result.WinningAgentCount);
        Assert.Equal(64_000, result.WinningAgentTotal);
        Assert.Equal(32_000, result.AmountPerWinningAgent);
        Assert.Equal(36_000, result.ParticipationPool);
        Assert.Equal(12_000, result.AmountPerOtherAgent);
    }

    [Fact]
    public async Task Manager_can_record_and_reconcile_agent_payouts()
    {
        var winner = ActiveUser("winner", UserRole.Agent);
        var participant = ActiveUser("participant", UserRole.SeniorAgent);
        var manager = ActiveUser("manager", UserRole.Manager);
        var fixture = new Fixture(winner, participant, manager);

        var records = await fixture.Service.CreateSettlementAsync(
            new CreateAuctionSettlementRequest("Rockford 7", 110_000, 10_000, [winner.Id], [participant.Id]),
            manager.Id,
            default);
        var paid = await fixture.Service.SetPaidAsync(records[0].Id, true, manager.Id, default);

        Assert.Equal(2, records.Count);
        Assert.Equal(28_000, records.Single(x => x.IsWinningAgent).CommissionAmount);
        Assert.Equal(12_000, records.Single(x => !x.IsWinningAgent).CommissionAmount);
        Assert.True(paid.IsPaid);
    }

    [Fact]
    public async Task Manager_can_update_and_delete_an_unpaid_settlement()
    {
        var firstWinner = ActiveUser("winner-1", UserRole.Agent);
        var secondWinner = ActiveUser("winner-2", UserRole.Agent);
        var participant = ActiveUser("participant", UserRole.SeniorAgent);
        var manager = ActiveUser("manager", UserRole.Manager);
        var fixture = new Fixture(firstWinner, secondWinner, participant, manager);
        var created = await fixture.Service.CreateSettlementAsync(
            new CreateAuctionSettlementRequest("Rockford 7", 510_000, 10_000, [firstWinner.Id], [participant.Id]),
            manager.Id,
            default);

        var updated = await fixture.Service.UpdateSettlementAsync(
            created[0].SettlementId,
            new CreateAuctionSettlementRequest("Rockford 8", 510_000, 10_000, [firstWinner.Id, secondWinner.Id], [participant.Id]),
            manager.Id,
            default);
        await fixture.Service.DeleteSettlementAsync(created[0].SettlementId, manager.Id, default);
        var overview = await fixture.Service.GetOverviewAsync(manager.Id, default);

        Assert.Equal(3, updated.Count);
        Assert.Equal(2, updated.Count(x => x.IsWinningAgent));
        Assert.All(updated.Where(x => x.IsWinningAgent), record => Assert.Equal(32_000, record.CommissionAmount));
        Assert.Equal(36_000, updated.Single(x => !x.IsWinningAgent).CommissionAmount);
        Assert.Empty(overview.Records);
    }

    [Fact]
    public async Task Paid_settlement_must_be_marked_unpaid_before_editing_or_deleting()
    {
        var winner = ActiveUser("winner", UserRole.Agent);
        var manager = ActiveUser("manager", UserRole.Manager);
        var fixture = new Fixture(winner, manager);
        var records = await fixture.Service.CreateSettlementAsync(
            new CreateAuctionSettlementRequest("Rockford 7", 110_000, 10_000, [winner.Id], []),
            manager.Id,
            default);
        await fixture.Service.SetPaidAsync(records[0].Id, true, manager.Id, default);

        var exception = await Assert.ThrowsAsync<DomainRuleException>(() =>
            fixture.Service.DeleteSettlementAsync(records[0].SettlementId, manager.Id, default));

        Assert.Equal("SETTLEMENT_HAS_PAID_PAYOUTS", exception.ErrorCode);
    }

    private static User ActiveUser(string id, UserRole role) => new()
    {
        Id = id,
        DisplayName = id,
        Role = role,
        ApprovalStatus = ApprovalStatus.Approved,
        AccessStatus = AccessStatus.Active
    };

    private sealed class Fixture
    {
        public Fixture(params User[] users)
        {
            Service = new CommissionService(
                new FakeCommissionRepository(),
                new FakeUserRepository(users),
                new FakeAuditRepository());
        }
        public CommissionService Service { get; }
    }

    private sealed class FakeCommissionRepository : ICommissionRepository
    {
        private readonly List<CommissionRecord> _values = [];
        public Task CreateManyAsync(IReadOnlyCollection<CommissionRecord> values, CancellationToken ct)
        {
            foreach (var value in values)
            {
                value.Id = Guid.NewGuid().ToString("N");
                value.CreatedAt = DateTime.UtcNow;
                _values.Add(value);
            }
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<CommissionRecord>> GetAllAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CommissionRecord>>(_values.Where(x => !x.IsDeleted).ToList());
        public Task<IReadOnlyList<CommissionRecord>> GetByAgentAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<CommissionRecord>>(_values.Where(x => !x.IsDeleted && x.AgentUserId == id).ToList());
        public Task<IReadOnlyList<CommissionRecord>> GetBySettlementIdAsync(string id, CancellationToken ct) => Task.FromResult<IReadOnlyList<CommissionRecord>>(_values.Where(x => !x.IsDeleted && x.SettlementId == id).ToList());
        public Task<CommissionRecord?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_values.FirstOrDefault(x => !x.IsDeleted && x.Id == id));
        public Task UpdateAsync(CommissionRecord value, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeUserRepository(IEnumerable<User> seed) : IUserRepository
    {
        private readonly List<User> _values = [.. seed];
        public Task<PagedResult<User>> QueryAsync(int page, int pageSize, ApprovalStatus? approval, AccessStatus? access, UserRole? role, CancellationToken ct)
        {
            var values = _values.Where(x => (!approval.HasValue || x.ApprovalStatus == approval) && (!access.HasValue || x.AccessStatus == access) && (!role.HasValue || x.Role == role)).ToList();
            return Task.FromResult(new PagedResult<User>(values, page, pageSize, values.Count));
        }
        public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(_values.FirstOrDefault(x => x.Id == id));
        public Task<User?> GetByDiscordIdAsync(string id, CancellationToken ct) => Task.FromResult(_values.FirstOrDefault(x => x.DiscordUserId == id));
        public Task<User?> GetByCidAsync(int cid, CancellationToken ct) => Task.FromResult(_values.FirstOrDefault(x => x.Cid == cid));
        public Task<long> CountActiveManagersAsync(CancellationToken ct) => Task.FromResult(0L);
        public Task<long> CountPendingAsync(CancellationToken ct) => Task.FromResult(0L);
        public Task CreateAsync(User user, CancellationToken ct) { _values.Add(user); return Task.CompletedTask; }
        public Task UpdateAsync(User user, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAuditRepository : IAuditRepository
    {
        public Task CreateAsync(AuditLog value, CancellationToken ct) => Task.CompletedTask;
        public Task<PagedResult<AuditLog>> QueryAsync(int page, int pageSize, CancellationToken ct) => Task.FromResult(new PagedResult<AuditLog>([], page, pageSize, 0));
    }
}
