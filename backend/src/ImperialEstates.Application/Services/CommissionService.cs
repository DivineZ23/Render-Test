using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Application.Services;

public sealed class CommissionService(
    ICommissionRepository commissions,
    IUserRepository users,
    IAuditRepository audits)
{
    private const decimal MaximumAdditionalPool = 250_000m;

    public static AuctionCommissionCalculationDto Calculate(
        decimal finalPrice,
        decimal basePrice,
        int totalAgents,
        int winningAgentCount = 1)
    {
        if (basePrice < 0) throw new DomainRuleException("Base price cannot be negative.", "INVALID_BASE_PRICE");
        if (finalPrice < basePrice)
            throw new DomainRuleException("Final auction price cannot be lower than the base price.", "FINAL_PRICE_BELOW_BASE");
        if (totalAgents < 1)
            throw new DomainRuleException("At least one participating agent is required.", "AGENT_REQUIRED");
        if (winningAgentCount < 1 || winningAgentCount > totalAgents)
            throw new DomainRuleException("Select at least one winner from the participating agents.", "INVALID_WINNER_COUNT");

        var premium = finalPrice - basePrice;
        var pool = Math.Min(CalculateAdditionalPool(premium), MaximumAdditionalPool);
        var otherAgentCount = totalAgents - winningAgentCount;
        var winningClosingShare = RoundMoney(pool * 0.60m);
        var participationPool = otherAgentCount > 0 ? RoundMoney(pool - winningClosingShare) : 0m;
        var perWinningAgent = RoundMoney((basePrice + winningClosingShare) / winningAgentCount);
        var perOtherAgent = otherAgentCount > 0 ? RoundMoney(participationPool / otherAgentCount) : 0m;
        var agentDistribution = basePrice + winningClosingShare + participationPool;
        var managementRemainder = finalPrice - agentDistribution;
        if (RoundMoney(agentDistribution + managementRemainder) != RoundMoney(finalPrice))
            throw new InvalidOperationException("Auction settlement does not balance to the final price.");

        return new AuctionCommissionCalculationDto(
            finalPrice,
            basePrice,
            premium,
            pool,
            managementRemainder,
            basePrice,
            winningClosingShare,
            basePrice + winningClosingShare,
            perWinningAgent,
            participationPool,
            perOtherAgent,
            totalAgents,
            winningAgentCount,
            otherAgentCount);
    }

    public Task<AuctionCommissionCalculationDto> PreviewAsync(PreviewAuctionCommissionRequest request) =>
        Task.FromResult(Calculate(
            request.FinalAuctionPrice,
            request.BasePrice,
            request.TotalNumberOfAgents,
            request.WinningAgentCount));

    public async Task<IReadOnlyList<CommissionRecordDto>> CreateSettlementAsync(
        CreateAuctionSettlementRequest request,
        string actorId,
        CancellationToken ct)
    {
        await EnsureManagerAsync(actorId, ct);
        var settlementId = Guid.NewGuid().ToString("N");
        var records = await BuildSettlementRecordsAsync(settlementId, request, actorId, ct);
        await commissions.CreateManyAsync(records, ct);
        await AuditSettlementAsync("commission.auction-settlement.created", settlementId, request, records.Count, actorId, ct);
        return records.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<CommissionRecordDto>> UpdateSettlementAsync(
        string settlementId,
        CreateAuctionSettlementRequest request,
        string actorId,
        CancellationToken ct)
    {
        await EnsureManagerAsync(actorId, ct);
        var existing = await GetEditableSettlementAsync(settlementId, ct);
        var replacements = await BuildSettlementRecordsAsync(settlementId, request, actorId, ct);
        var originalCreatedAt = existing.Min(x => x.CreatedAt);
        var originalCreatedBy = existing[0].CreatedBy;
        foreach (var replacement in replacements)
        {
            replacement.CreatedAt = originalCreatedAt;
            replacement.CreatedBy = originalCreatedBy;
            replacement.UpdatedBy = actorId;
        }

        await SoftDeleteRecordsAsync(existing, actorId, ct);
        try
        {
            await commissions.CreateManyAsync(replacements, ct);
        }
        catch
        {
            foreach (var record in existing)
            {
                record.IsDeleted = false;
                record.UpdatedBy = actorId;
                record.UpdatedAt = DateTime.UtcNow;
                await commissions.UpdateAsync(record, ct);
            }
            throw;
        }
        await AuditSettlementAsync("commission.auction-settlement.updated", settlementId, request, replacements.Count, actorId, ct);
        return replacements.Select(ToDto).ToList();
    }

    public async Task DeleteSettlementAsync(string settlementId, string actorId, CancellationToken ct)
    {
        await EnsureManagerAsync(actorId, ct);
        var existing = await GetEditableSettlementAsync(settlementId, ct);
        await SoftDeleteRecordsAsync(existing, actorId, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = "commission.auction-settlement.deleted",
            EntityType = "commission_settlement",
            EntityId = settlementId,
            PerformedByUserId = actorId,
            Metadata = new()
            {
                ["auctionReference"] = existing[0].AuctionReference,
                ["agentCount"] = existing.Count
            }
        }, ct);
    }

    public async Task<CommissionOverviewDto> GetOverviewAsync(string actorId, CancellationToken ct)
    {
        var actor = await GetUserAsync(actorId, ct);
        var canManage = actor.Role is UserRole.Manager or UserRole.Owner;
        var records = canManage
            ? await commissions.GetAllAsync(ct)
            : await commissions.GetByAgentAsync(actorId, ct);

        var eligibleUsers = new List<User>();
        if (canManage)
        {
            eligibleUsers.AddRange((await users.QueryAsync(1, 100, ApprovalStatus.Approved, AccessStatus.Active, UserRole.Agent, ct)).Items);
            eligibleUsers.AddRange((await users.QueryAsync(1, 100, ApprovalStatus.Approved, AccessStatus.Active, UserRole.SeniorAgent, ct)).Items);
        }
        else if (actor.Role is UserRole.Agent or UserRole.SeniorAgent)
        {
            eligibleUsers.Add(actor);
        }

        var summaries = eligibleUsers
            .GroupBy(x => x.Id)
            .Select(group =>
            {
                var user = group.First();
                var agentRecords = records.Where(x => x.AgentUserId == user.Id).ToList();
                return new AgentCommissionSummaryDto(
                    user.Id,
                    user.DisplayName,
                    user.Role,
                    agentRecords.Sum(x => x.CommissionAmount),
                    agentRecords.Where(x => !x.IsPaid).Sum(x => x.CommissionAmount),
                    agentRecords.Where(x => x.IsPaid).Sum(x => x.CommissionAmount),
                    agentRecords.Select(x => x.SettlementId).Distinct().Count(),
                    agentRecords.Count(x => !x.IsPaid));
            })
            .OrderByDescending(x => x.OutstandingCommission)
            .ThenBy(x => x.DisplayName)
            .ToList();

        return new CommissionOverviewDto(
            summaries,
            records.Select(ToDto).ToList(),
            records.Where(x => !x.IsPaid).Sum(x => x.CommissionAmount),
            records.Where(x => x.IsPaid).Sum(x => x.CommissionAmount));
    }

    public async Task<CommissionRecordDto> SetPaidAsync(
        string id,
        bool isPaid,
        string actorId,
        CancellationToken ct)
    {
        var actor = await GetUserAsync(actorId, ct);
        if (actor.Role is not (UserRole.Manager or UserRole.Owner))
            throw new DomainRuleException("Only managers and owners can update commission payments.", "MANAGER_REQUIRED");
        var value = await commissions.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Commission record not found.");
        value.IsPaid = isPaid;
        value.PaidAt = isPaid ? DateTime.UtcNow : null;
        value.PaidByUserId = isPaid ? actorId : null;
        value.PaidByDisplayName = isPaid ? actor.DisplayName : null;
        value.UpdatedBy = actorId;
        value.UpdatedAt = DateTime.UtcNow;
        await commissions.UpdateAsync(value, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = isPaid ? "commission.paid" : "commission.marked-unpaid",
            EntityType = "commission",
            EntityId = value.Id,
            PerformedByUserId = actorId,
            Metadata = new() { ["agentUserId"] = value.AgentUserId, ["amount"] = value.CommissionAmount }
        }, ct);
        return ToDto(value);
    }

    private static decimal CalculateAdditionalPool(decimal premium)
    {
        var remaining = premium;
        var pool = Take(ref remaining, 100_000m) * 0.30m;
        pool += Take(ref remaining, 200_000m) * 0.20m;
        pool += remaining * 0.10m;
        return RoundMoney(pool);
    }

    private static decimal Take(ref decimal remaining, decimal maximum)
    {
        var amount = Math.Min(Math.Max(remaining, 0), maximum);
        remaining -= amount;
        return amount;
    }

    private static decimal RoundMoney(decimal value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private static CommissionRecord CreateRecord(
        string settlementId,
        string auctionReference,
        User agent,
        bool isWinner,
        AuctionCommissionCalculationDto calculation,
        decimal baseShare,
        decimal premiumShare,
        string actorId) => new()
        {
            SchemeVersion = CommissionRecord.CurrentSchemeVersion,
            TenantId = $"{settlementId}:{Guid.NewGuid():N}:{agent.Id}",
            SettlementId = settlementId,
            AuctionReference = auctionReference.Trim(),
            AgentUserId = agent.Id,
            AgentDisplayName = agent.DisplayName,
            AgentRole = agent.Role,
            IsWinningAgent = isWinner,
            TotalAgentCount = calculation.TotalAgentCount,
            FinalAuctionPrice = calculation.FinalAuctionPrice,
            BasePrice = calculation.BasePrice,
            AuctionPremium = calculation.AuctionPremium,
            AdditionalAgentPool = calculation.AdditionalAgentPool,
            BaseShare = baseShare,
            PremiumShare = premiumShare,
            CommissionAmount = baseShare + premiumShare,
            CreatedBy = actorId
        };

    private async Task<List<CommissionRecord>> BuildSettlementRecordsAsync(
        string settlementId,
        CreateAuctionSettlementRequest request,
        string actorId,
        CancellationToken ct)
    {
        var winnerIds = request.WinningAgentUserIds
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (winnerIds.Count == 0)
            throw new DomainRuleException("Select at least one winning agent.", "WINNER_REQUIRED");

        var winnerIdSet = winnerIds.ToHashSet(StringComparer.Ordinal);
        var otherIds = request.OtherAgentUserIds
            .Where(x => !string.IsNullOrWhiteSpace(x) && !winnerIdSet.Contains(x))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var calculation = Calculate(
            request.FinalAuctionPrice,
            request.BasePrice,
            winnerIds.Count + otherIds.Count,
            winnerIds.Count);

        var winners = new List<User>();
        foreach (var id in winnerIds) winners.Add(await GetEligibleAgentAsync(id, ct));
        var others = new List<User>();
        foreach (var id in otherIds) others.Add(await GetEligibleAgentAsync(id, ct));

        var records = new List<CommissionRecord>();
        var allocatedBase = 0m;
        var allocatedWinnerPremium = 0m;
        for (var index = 0; index < winners.Count; index++)
        {
            var baseShare = index == winners.Count - 1
                ? calculation.WinningAgentBaseShare - allocatedBase
                : RoundMoney(calculation.WinningAgentBaseShare / winners.Count);
            var premiumShare = index == winners.Count - 1
                ? calculation.WinningAgentClosingShare - allocatedWinnerPremium
                : RoundMoney(calculation.WinningAgentClosingShare / winners.Count);
            allocatedBase += baseShare;
            allocatedWinnerPremium += premiumShare;
            records.Add(CreateRecord(
                settlementId,
                request.AuctionReference,
                winners[index],
                true,
                calculation,
                baseShare,
                premiumShare,
                actorId));
        }

        var allocatedParticipation = 0m;
        for (var index = 0; index < others.Count; index++)
        {
            var share = index == others.Count - 1
                ? calculation.ParticipationPool - allocatedParticipation
                : calculation.AmountPerOtherAgent;
            allocatedParticipation += share;
            records.Add(CreateRecord(
                settlementId,
                request.AuctionReference,
                others[index],
                false,
                calculation,
                0,
                share,
                actorId));
        }

        return records;
    }

    private async Task<IReadOnlyList<CommissionRecord>> GetEditableSettlementAsync(
        string settlementId,
        CancellationToken ct)
    {
        var records = await commissions.GetBySettlementIdAsync(settlementId, ct);
        if (records.Count == 0) throw new KeyNotFoundException("Commission settlement not found.");
        if (records.Any(x => x.IsPaid))
            throw new DomainRuleException(
                "Mark every payout in this settlement as unpaid before editing or deleting it.",
                "SETTLEMENT_HAS_PAID_PAYOUTS");
        return records;
    }

    private async Task SoftDeleteRecordsAsync(
        IReadOnlyList<CommissionRecord> records,
        string actorId,
        CancellationToken ct)
    {
        foreach (var record in records)
        {
            record.IsDeleted = true;
            record.UpdatedBy = actorId;
            record.UpdatedAt = DateTime.UtcNow;
            await commissions.UpdateAsync(record, ct);
        }
    }

    private async Task AuditSettlementAsync(
        string action,
        string settlementId,
        CreateAuctionSettlementRequest request,
        int agentCount,
        string actorId,
        CancellationToken ct) =>
        await audits.CreateAsync(new AuditLog
        {
            Action = action,
            EntityType = "commission_settlement",
            EntityId = settlementId,
            PerformedByUserId = actorId,
            Metadata = new()
            {
                ["auctionReference"] = request.AuctionReference,
                ["finalAuctionPrice"] = request.FinalAuctionPrice,
                ["basePrice"] = request.BasePrice,
                ["winnerCount"] = request.WinningAgentUserIds.Count,
                ["agentCount"] = agentCount
            }
        }, ct);

    private async Task EnsureManagerAsync(string actorId, CancellationToken ct)
    {
        var actor = await GetUserAsync(actorId, ct);
        if (actor.Role is not (UserRole.Manager or UserRole.Owner))
            throw new DomainRuleException(
                "Only managers and owners can manage auction settlements.",
                "MANAGER_REQUIRED");
    }

    private static CommissionRecordDto ToDto(CommissionRecord value) => new(
        value.Id,
        value.SettlementId,
        value.AuctionReference,
        value.AgentUserId,
        value.AgentDisplayName,
        value.AgentRole,
        value.IsWinningAgent,
        value.TotalAgentCount,
        value.FinalAuctionPrice,
        value.BasePrice,
        value.AuctionPremium,
        value.AdditionalAgentPool,
        value.BaseShare,
        value.PremiumShare,
        value.CommissionAmount,
        value.IsPaid,
        value.PaidAt,
        value.PaidByUserId,
        value.PaidByDisplayName,
        value.CreatedAt);

    private async Task<User> GetEligibleAgentAsync(string id, CancellationToken ct)
    {
        var user = await GetUserAsync(id, ct);
        if (user.Role is not (UserRole.Agent or UserRole.SeniorAgent) ||
            user.ApprovalStatus != ApprovalStatus.Approved || user.AccessStatus != AccessStatus.Active)
            throw new DomainRuleException("Only approved, active agents can participate in a settlement.", "AGENT_NOT_ELIGIBLE");
        return user;
    }

    private async Task<User> GetUserAsync(string id, CancellationToken ct) =>
        await users.GetByIdAsync(id, ct) ?? throw new UnauthorizedAccessException();
}
