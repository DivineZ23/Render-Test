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
