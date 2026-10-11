using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Application.Services;

public sealed class AgentAnalyticsService(
    IUserRepository users,
    IPropertyBookingRepository bookings,
    ITenantRepository tenants,
    ICommissionRepository commissions,
    IRentSyncRepository rentSync)
{
    public async Task<AgentAnalyticsDto> GetAsync(DateTime? from, DateTime? to, CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new DomainRuleException("The start date cannot be after the end date.", "INVALID_ANALYTICS_RANGE");

        var agentsTask = GetApprovedAgentsAsync(UserRole.Agent, ct);
        var seniorAgentsTask = GetApprovedAgentsAsync(UserRole.SeniorAgent, ct);
        var bookingsTask = bookings.GetAllAsync(ct);
        var tenantsTask = tenants.GetAllAsync(ct);
        var commissionsTask = commissions.GetAllAsync(ct);
        var snapshotsTask = rentSync.GetAllAsync(ct);

        await Task.WhenAll(
            agentsTask,
            seniorAgentsTask,
            bookingsTask,
            tenantsTask,
            commissionsTask,
            snapshotsTask);

        var team = agentsTask.Result
            .Concat(seniorAgentsTask.Result)
            .GroupBy(user => user.Id, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        var bookingValues = bookingsTask.Result
            .Where(value => InRange(value.CreatedAt, from, to))
            .ToList();
        var saleValues = tenantsTask.Result
            .Where(value => InRange(value.CreatedAt, from, to))
            .ToList();
        var evictionValues = tenantsTask.Result
            .Where(value => value.EndDate.HasValue && InRange(value.EndDate.Value, from, to))
            .ToList();
        var commissionValues = commissionsTask.Result.ToList();
        var noticeValues = snapshotsTask.Result
            .SelectMany(snapshot => snapshot.Records)
            .Where(record =>
                record.Status is "overdue" or "evictable" &&
                record.ResolvedAt.HasValue &&
                InRange(record.ResolvedAt.Value, from, to))
            .ToList();

        var rows = team.Select(agent =>
        {
            var agentBookings = bookingValues
                .Where(value => value.CreatedBy == agent.Id)
                .ToList();
            var agentCommissions = commissionValues
                .Where(value => value.AgentUserId == agent.Id)
                .ToList();
            var receivedCommission = agentCommissions
                .Where(value => value.IsPaid && value.PaidAt.HasValue && InRange(value.PaidAt.Value, from, to))
                .Sum(value => value.CommissionAmount);
            var outstandingCommission = agentCommissions
                .Where(value => !value.IsPaid && InRange(value.CreatedAt, from, to))
                .Sum(value => value.CommissionAmount);
            var housesSold = saleValues.Count(value => value.CreatedBy == agent.Id);
            var convertedBookings = agentBookings.Count(value => value.Status == BookingStatus.Converted);

            return new AgentRow(
                agent,
                agentBookings.Count,
                agentBookings.Sum(value => value.BookingAmount),
                convertedBookings,
                housesSold,
                receivedCommission,
                outstandingCommission,
                evictionValues.Count(value => value.EvictedByUserId == agent.Id),
                noticeValues.Count(value => value.ResolvedByUserId == agent.Id));
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

    private static bool InRange(DateTime value, DateTime? from, DateTime? to) =>
        (!from.HasValue || value >= from.Value) && (!to.HasValue || value <= to.Value);

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
