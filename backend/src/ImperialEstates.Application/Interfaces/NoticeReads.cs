using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Application.Interfaces;

// Read models keep export history out of page responses and tenant lookups.
public sealed record NoticeHistoryRow(string SnapshotId, DateTime SyncedAt, RentSyncRecord Record);
public sealed record NoticeSnapshotCounts(DateTime SyncedAt, int Current, int Total, int Pending, int SentToday);
public sealed record RentRecordPage(RentSyncSnapshot Snapshot, int Total, int Active, int Overdue, int Evictable, int Empty, int Matching);
public sealed record EvictionPropertyLookup(string Id, int PropertyId, string PropertyName, PropertyStatus Status, string? CurrentTenantId);
