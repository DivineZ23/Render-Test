namespace ImperialEstates.Application.DTOs;

public sealed record RentSyncRequest(string RawData);
public sealed record SetNoticeResolutionRequest(bool IsResolved);
public sealed record SetEvictionHoldRequest(bool IsOnHold);

public sealed record NoticeFeedItemDto(string SnapshotId, DateTime SyncedAt, RentSyncRecordDto Record);
public sealed record NoticeFeedDto(DateTime AsOf, IReadOnlyList<NoticeFeedItemDto> Items, bool HasMore, int NextSkip);
public sealed record NoticeFeedSummaryDto(DateTime? SyncedAt, int CurrentTotal, int TotalNotices, int Pending, int SentToday);
public sealed record RentRecordPageDto(RentSyncSnapshotDto Snapshot, bool HasMore, int NextSkip);

public sealed record EvictionQueueItemDto(
    string PropertyId, int PropertyBusinessId, string PropertyName, string NoticeSnapshotId, int NoticeRowNumber,
    string? TenantName, int? Cid, string? Phone, string? DiscordId, decimal Rent,
    DateTime NoticeSentAt, DateTime EligibleAt, bool IsReady, bool IsOnHold,
    string? HeldByUserId, string? HeldByDisplayName, DateTime? HeldAt);

public sealed record RentSyncRecordDto(
    int RowNumber, string Status, DateTime? PaidThrough, string Address, string Interior,
    int? Cid, string? RenterName, string? Phone, decimal Income, decimal Cost,
    string? TenantId, string? DiscordId, bool TenantMatched,
    string? OverdueNotice, string? EvictionNotice, bool IsResolved,
    string? ResolvedByUserId, string? ResolvedByDisplayName, DateTime? ResolvedAt,
    DateTime? LastPaid = null, DateTime? NextCharge = null);

public sealed record RentSyncSnapshotDto(
    string Id, string? CreatedBy, DateTime? SyncedAt, int Total, int Active, int Overdue, int Evictable, int Empty,
    int UnmappedTenants, string GoogleSheetSyncStatus, DateTime? GoogleSheetSyncedAt,
    string? GoogleSheetSyncError, string? GoogleSheetUrl, IReadOnlyList<RentSyncRecordDto> Records);
