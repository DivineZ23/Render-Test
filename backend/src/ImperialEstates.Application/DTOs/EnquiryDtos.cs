using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Application.DTOs;

public sealed record CreateEnquiryRequest(
    string PropertyId, string FullName, string PhoneNumber, string? Email, string? DiscordUsername,
    string? Message, string? PreferredContactMethod);

public sealed record EnquiryDto(
    string Id, string PropertyId, string PropertyName, string FullName, string PhoneNumber, string? Email,
    string? DiscordUsername, string? Message, string? PreferredContactMethod, EnquiryStatus Status,
    string? AssignedAgentId, string? InternalNotes, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record UpdateEnquiryRequest(EnquiryStatus? Status, string? AssignedAgentId, string? InternalNotes);

public sealed record TenantDto(
    string Id, string PropertyId, string FullName, string PhoneNumber, string? Email, DateTime StartDate,
    DateTime? ExpectedEndDate, DateTime? EndDate, decimal MonthlyRent, string Status, DateTime CreatedAt);

public sealed record TenantSummaryDto(
    string Id, int? Cid, string FullName, string PhoneNumber, string DiscordId,
    int PropertyCount, decimal TotalRent, string Status);

public sealed record EvictionHistoryDto(
    string Id, string PropertyId, int? PropertyBusinessId, string? PropertyName,
    string TenantName, int? Cid, string PhoneNumber, decimal MonthlyRent,
    string Reason, IReadOnlyList<string> StorageImageUrls, string? EvictedByUserId,
    string? EvictedByDisplayName, DateTime EvictedAt);

public sealed class AuditLogQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
    public string? Search { get; init; }
    public string? Category { get; init; }
    public string? EntityType { get; init; }
    public string? ActorId { get; init; }
    public DateTime? From { get; init; }
    public DateTime? To { get; init; }
    public string SortDirection { get; init; } = "desc";

    // Populated by AuditLogService so searches also match readable user/entity names.
    public IReadOnlyCollection<string> MatchingActorIds { get; init; } = [];
    public IReadOnlyCollection<string> MatchingEntityIds { get; init; } = [];
}

public sealed record AuditLogDto(
    string Id,
    string Action,
    string ActionLabel,
    string Category,
    string Severity,
    string Summary,
    string EntityType,
    string EntityTypeLabel,
    string EntityId,
    string EntityDisplayName,
    string PerformedByUserId,
    string PerformedByDisplayName,
    string? PerformedByAvatarUrl,
    string? PerformedByRole,
    IReadOnlyDictionary<string, object?>? PreviousValues,
    IReadOnlyDictionary<string, object?>? NewValues,
    IReadOnlyDictionary<string, object?>? Metadata,
    DateTime CreatedAt);

public sealed record DashboardSummaryDto(
    long TotalBlocks, long TotalProperties, long AvailableProperties, long BookedProperties,
    long OccupiedProperties, decimal TotalRevenue, decimal TotalCost, decimal TotalProfit,
    decimal AverageProfitPerProperty, string? MostProfitableBlock, decimal MostProfitableBlockProfit,
    long PendingEnquiries, long PendingUsers,
    IReadOnlyList<PropertyStatusHistoryDto> RecentStatusChanges);

public sealed record PersonalActivityDto(
    string Id, string PropertyId, string PropertyName, string TenantName, int? Cid,
    decimal Amount, DateTime OccurredAt);

public sealed record PersonalStatisticsDto(
    int HousesSold, int HousesEvicted, decimal TotalDepositTaken,
    IReadOnlyList<PersonalActivityDto> RecentSales,
    IReadOnlyList<PersonalActivityDto> RecentEvictions);

public sealed record AgentPerformanceDto(
    string UserId,
    string DisplayName,
    string? FullName,
    string? AvatarUrl,
    UserRole Role,
    int Bookings,
    decimal BookingAmount,
    int ConvertedBookings,
    int HousesSold,
    decimal CommissionTaken,
    decimal CommissionOwed,
    int HousesEvicted,
    int NoticesSent,
    decimal ConversionRate,
    decimal PerformanceScore);

public sealed record AgentAnalyticsTotalsDto(
    int Agents,
    int Bookings,
    decimal BookingAmount,
    int HousesSold,
    decimal CommissionTaken,
    decimal CommissionOwed,
    int HousesEvicted,
    int NoticesSent);

public sealed record AgentAnalyticsDto(
    DateTime? From,
    DateTime? To,
    AgentAnalyticsTotalsDto Totals,
    IReadOnlyList<AgentPerformanceDto> Agents);

public sealed record AgentBookingActivityDto(
    string Id, string PropertyId, string PropertyName, string ClientName,
    decimal BookingAmount, decimal MonthlyRent, BookingStatus Status, DateTime CreatedAt);

public sealed record AgentSaleActivityDto(
    string Id, string PropertyId, string PropertyName, string TenantName,
    decimal MonthlyRent, decimal SecurityDeposit, DateTime SoldAt);

public sealed record AgentCommissionActivityDto(
    string Id, string AuctionReference, decimal Amount, bool IsPaid,
    bool IsWinningAgent, DateTime CreatedAt, DateTime? PaidAt);

public sealed record AgentEvictionActivityDto(
    string Id, string PropertyId, string PropertyName, string TenantName,
    string Reason, DateTime EvictedAt);

public sealed record AgentNoticeActivityDto(
    string Id, string Address, string Status, string? RenterName,
    int? Cid, decimal Rent, DateTime SentAt);

public sealed record AgentAnalyticsDetailDto(
    DateTime? From,
    DateTime? To,
    AgentPerformanceDto Summary,
    IReadOnlyList<AgentBookingActivityDto> Bookings,
    IReadOnlyList<AgentSaleActivityDto> Sales,
    IReadOnlyList<AgentCommissionActivityDto> Commissions,
    IReadOnlyList<AgentEvictionActivityDto> Evictions,
    IReadOnlyList<AgentNoticeActivityDto> Notices);
