using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Application.Services;

public sealed class AgentAnalyticsService(
    IUserRepository users,
    IPropertyBookingRepository bookings,
    IPropertyRepository properties,
    ITenantRepository tenants,
    ICommissionRepository commissions,
    IRentSyncRepository rentSync)
{
    public async Task<AgentAnalyticsDto> GetAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        var data = await LoadAsync(from, to, ct);
        return BuildReport(data, from, to);
    }

    private async Task<AnalyticsData> LoadAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new DomainRuleException("The start date cannot be after the end date.", "INVALID_ANALYTICS_RANGE");

        var agentsTask = GetApprovedAgentsAsync(UserRole.Agent, ct);
        var seniorAgentsTask = GetApprovedAgentsAsync(UserRole.SeniorAgent, ct);
        var managersTask = GetApprovedAgentsAsync(UserRole.Manager, ct);
        var bookingsTask = bookings.GetForAnalyticsAsync(from, to, ct);
        var tenantsTask = tenants.GetForAnalyticsAsync(from, to, ct);
        var commissionsTask = commissions.GetForAnalyticsAsync(from, to, ct);
        var snapshotsTask = rentSync.GetNoticesForAnalyticsAsync(from, to, ct);

        await Task.WhenAll(
            agentsTask,
            seniorAgentsTask,
            managersTask,
            bookingsTask,
            tenantsTask,
            commissionsTask,
            snapshotsTask);

        var team = agentsTask.Result
            .Concat(seniorAgentsTask.Result)
            .Concat(managersTask.Result)
            .GroupBy(user => user.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        return new AnalyticsData(team, bookingsTask.Result, tenantsTask.Result,
            commissionsTask.Result, snapshotsTask.Result);
    }

    private static AgentAnalyticsDto BuildReport(AnalyticsData data, DateTime? from, DateTime? to)
    {
        var bookingValues = data.Bookings
            .Where(value => InRange(value.CreatedAt, from, to))
            .ToLookup(value => value.CreatedBy ?? string.Empty);
        var saleValues = data.Tenants
            .Where(value => InRange(value.CreatedAt, from, to))
            .ToLookup(value => value.CreatedBy ?? string.Empty);
        var evictionValues = data.Tenants
            .Where(value => value.EndDate.HasValue && InRange(value.EndDate.Value, from, to))
            .ToLookup(value => value.EvictedByUserId ?? string.Empty);
        var commissionValues = data.Commissions.ToLookup(value => value.AgentUserId);
        var noticeValues = data.Snapshots
            .SelectMany(snapshot => snapshot.Records)
            .Where(record =>
                record.Status is "overdue" or "evictable" &&
                record.ResolvedAt.HasValue &&
                InRange(record.ResolvedAt.Value, from, to))
            .ToLookup(value => value.ResolvedByUserId ?? string.Empty);

        var rows = data.Team.Select(agent =>
        {
            var agentBookings = bookingValues[agent.Id].ToList();
            var agentCommissions = commissionValues[agent.Id];
            var receivedCommission = agentCommissions
                .Where(value => value.IsPaid && value.PaidAt.HasValue && InRange(value.PaidAt.Value, from, to))
                .Sum(value => value.CommissionAmount);
            var outstandingCommission = agentCommissions
                .Where(value => !value.IsPaid && InRange(value.CreatedAt, from, to))
                .Sum(value => value.CommissionAmount);
            var housesSold = saleValues[agent.Id].Count();
            var convertedBookings = agentBookings.Count(value => value.Status == BookingStatus.Converted);

            return new AgentRow(
                agent,
                agentBookings.Count,
                agentBookings.Sum(value => value.BookingAmount),
                convertedBookings,
                housesSold,
                receivedCommission,
                outstandingCommission,
                evictionValues[agent.Id].Count(),
                noticeValues[agent.Id].Count());
        }).ToList();

        var maximumBookings = rows.MaxOrDefault(row => row.Bookings);
        var maximumBookingAmount = rows.MaxOrDefault(row => row.BookingAmount);
        var maximumSales = rows.MaxOrDefault(row => row.HousesSold);
        var maximumCommission = rows.MaxOrDefault(row => row.CommissionTaken);
        var maximumEvictions = rows.MaxOrDefault(row => row.HousesEvicted);
        var maximumNotices = rows.MaxOrDefault(row => row.NoticesSent);

        var performance = rows
            .Select(row => new AgentPerformanceDto(
                row.User.Id,
                string.IsNullOrWhiteSpace(row.User.FullName) ? row.User.DisplayName : row.User.FullName,
                row.User.FullName,
                row.User.AvatarUrl,
                row.User.Role,
                row.Bookings,
                row.BookingAmount,
                row.ConvertedBookings,
                row.HousesSold,
                row.CommissionTaken,
                row.CommissionOwed,
                row.HousesEvicted,
                row.NoticesSent,
                Percentage(row.ConvertedBookings, row.Bookings),
                Score(row, maximumBookings, maximumBookingAmount, maximumSales,
                    maximumCommission, maximumEvictions, maximumNotices)))
            .OrderByDescending(row => row.PerformanceScore)
            .ThenByDescending(row => row.HousesSold)
            .ThenBy(row => row.DisplayName)
            .ToList();

        var totals = new AgentAnalyticsTotalsDto(
            performance.Count,
            performance.Sum(row => row.Bookings),
            performance.Sum(row => row.BookingAmount),
            performance.Sum(row => row.HousesSold),
            performance.Sum(row => row.CommissionTaken),
            performance.Sum(row => row.CommissionOwed),
            performance.Sum(row => row.HousesEvicted),
            performance.Sum(row => row.NoticesSent));

        return new AgentAnalyticsDto(from, to, totals, performance);
    }

    public async Task<AgentAnalyticsDetailDto> GetDetailAsync(
        string userId, DateTime? from, DateTime? to, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(userId, ct)
            ?? throw new KeyNotFoundException("Agent not found.");
        if (user.Role is not (UserRole.Agent or UserRole.SeniorAgent or UserRole.Manager) ||
            user.ApprovalStatus != ApprovalStatus.Approved ||
            user.AccessStatus != AccessStatus.Active)
            throw new KeyNotFoundException("Agent not found.");

        // The team-relative score and detail history share one set of source reads.
        var data = await LoadAsync(from, to, ct);
        var summary = BuildReport(data, from, to).Agents.FirstOrDefault(value => value.UserId == userId)
            ?? throw new KeyNotFoundException("Agent not found.");
        var propertyIds = data.Bookings.Where(value => value.CreatedBy == userId).Select(value => value.PropertyId)
            .Concat(data.Tenants.Where(value => value.CreatedBy == userId || value.EvictedByUserId == userId)
                .Select(value => value.PropertyId)).Distinct().ToArray();
        var propertyNames = (await properties.GetByIdsAsync(propertyIds, ct)).ToDictionary(
            value => value.Id, value => value.PropertyName, StringComparer.Ordinal);
        string PropertyName(string propertyId, string? fallback = null) =>
            propertyNames.GetValueOrDefault(propertyId, fallback ?? "Unknown property");

        var bookingHistory = data.Bookings
            .Where(value => value.CreatedBy == userId && InRange(value.CreatedAt, from, to))
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new AgentBookingActivityDto(
                value.Id, value.PropertyId, PropertyName(value.PropertyId), value.FullName,
                value.BookingAmount, value.MonthlyRent, value.Status, value.CreatedAt))
            .ToList();
        var sales = data.Tenants
            .Where(value => value.CreatedBy == userId && InRange(value.CreatedAt, from, to))
            .OrderByDescending(value => value.CreatedAt)
            .Select(value => new AgentSaleActivityDto(
                value.Id, value.PropertyId, PropertyName(value.PropertyId), value.FullName,
                value.MonthlyRent, value.SecurityDeposit, value.CreatedAt))
            .ToList();
        var commissionHistory = data.Commissions
            .Where(value => value.AgentUserId == userId && (InRange(value.CreatedAt, from, to) ||
                value.PaidAt.HasValue && InRange(value.PaidAt.Value, from, to)))
            .OrderByDescending(value => value.PaidAt ?? value.CreatedAt)
            .Select(value => new AgentCommissionActivityDto(
                value.Id, value.AuctionReference, value.CommissionAmount, value.IsPaid,
                value.IsWinningAgent, value.CreatedAt, value.PaidAt))
            .ToList();
        var evictions = data.Tenants
            .Where(value => value.EvictedByUserId == userId && value.EndDate.HasValue &&
                InRange(value.EndDate.Value, from, to))
            .OrderByDescending(value => value.EndDate)
            .Select(value => new AgentEvictionActivityDto(
                value.Id, value.PropertyId,
                PropertyName(value.PropertyId, value.EvictedPropertyName), value.FullName,
                value.EndReason ?? "No reason recorded", value.EndDate!.Value))
            .ToList();
        var notices = data.Snapshots
            .SelectMany(snapshot => snapshot.Records.Select(record => (snapshot.Id, Record: record)))
            .Where(value => value.Record.ResolvedByUserId == userId &&
                value.Record.ResolvedAt.HasValue &&
                value.Record.Status is "overdue" or "evictable" &&
                InRange(value.Record.ResolvedAt.Value, from, to))
            .OrderByDescending(value => value.Record.ResolvedAt)
            .Select(value => new AgentNoticeActivityDto(
                $"{value.Id}:{value.Record.RowNumber}", value.Record.Address,
                value.Record.Status, value.Record.RenterName, value.Record.Cid,
                value.Record.Income, value.Record.ResolvedAt!.Value))
            .ToList();

        return new AgentAnalyticsDetailDto(
            from, to, summary, bookingHistory, sales, commissionHistory, evictions, notices);
    }

    private static bool InRange(DateTime value, DateTime? from, DateTime? to) =>
        (!from.HasValue || value >= from.Value) && (!to.HasValue || value <= to.Value);

    private sealed record AnalyticsData(
        IReadOnlyList<User> Team, IReadOnlyList<PropertyBooking> Bookings,
        IReadOnlyList<Tenant> Tenants, IReadOnlyList<CommissionRecord> Commissions,
        IReadOnlyList<RentSyncSnapshot> Snapshots);

    private async Task<IReadOnlyList<User>> GetApprovedAgentsAsync(UserRole role, CancellationToken ct)
    {
        var result = new List<User>();
        var page = 1;
        while (true)
        {
            var values = await users.QueryAsync(
                page, 100, ApprovalStatus.Approved, AccessStatus.Active, role, ct);
            result.AddRange(values.Items);
            if (!values.HasNextPage) return result;
            page++;
        }
    }

    private static decimal Percentage(int numerator, int denominator) =>
        denominator == 0 ? 0 : Math.Round(numerator * 100m / denominator, 1, MidpointRounding.AwayFromZero);

    private static decimal Score(
        AgentRow row,
        decimal maximumBookings,
        decimal maximumBookingAmount,
        decimal maximumSales,
        decimal maximumCommission,
        decimal maximumEvictions,
        decimal maximumNotices)
    {
        var score = Relative(row.Bookings, maximumBookings) * 15m
            + Relative(row.BookingAmount, maximumBookingAmount) * 15m
            + Relative(row.HousesSold, maximumSales) * 30m
            + Relative(row.CommissionTaken, maximumCommission) * 25m
            + Relative(row.HousesEvicted, maximumEvictions) * 5m
            + Relative(row.NoticesSent, maximumNotices) * 10m;
        return Math.Round(score, 1, MidpointRounding.AwayFromZero);
    }

    private static decimal Relative(decimal value, decimal maximum) =>
        maximum <= 0 ? 0 : value / maximum;

    private sealed record AgentRow(
        User User,
        int Bookings,
        decimal BookingAmount,
        int ConvertedBookings,
        int HousesSold,
        decimal CommissionTaken,
        decimal CommissionOwed,
        int HousesEvicted,
        int NoticesSent);
}

file static class AgentAnalyticsEnumerableExtensions
{
    public static decimal MaxOrDefault<T>(this IEnumerable<T> values, Func<T, decimal> selector) =>
        values.Select(selector).DefaultIfEmpty(0).Max();
}
