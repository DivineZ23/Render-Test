using System.Globalization;
using System.Text.RegularExpressions;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Application.Services;

public sealed class RentSyncService(
    IRentSyncRepository snapshots,
    ITenantRepository tenants,
    IPropertyRepository properties,
    IPropertyLifecycleStore lifecycle,
    IStatusHistoryRepository history,
    IUserRepository users,
    IGoogleSheetsSyncService googleSheets,
    IAuditRepository audits)
{
    private static readonly Regex RowPattern = new(
        @"^(?<status>[^,]+),(?<address>[^,]+),(?<interior>[^,]+),(?<cid>[^,]+),(?<name>[^,]+),(?<phone>[^,]+),(?<income>\$(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?),(?<cost>\$(?:\d{1,3}(?:,\d{3})+|\d+)(?:\.\d{1,2})?)(?:,(?<lastPaid>[^,]*),(?<nextCharge>[^,]*))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<RentSyncSnapshotDto> GetCurrentAsync(CancellationToken ct)
    {
        var snapshot = await snapshots.GetCurrentAsync(ct);
        if (snapshot is not null) await RefreshTenantMappingsAsync([snapshot], ct);
        return Map(snapshot);
    }

    public async Task<IReadOnlyList<RentSyncSnapshotDto>> GetAllAsync(CancellationToken ct)
    {
        var values = await snapshots.GetAllAsync(ct);
        await RefreshTenantMappingsAsync(values, ct);
        return values.Select(Map).ToList();
    }

    public Task<IReadOnlyList<RentSyncSnapshotDto>> GetSummariesAsync(CancellationToken ct) =>
        snapshots.GetSummariesAsync(ct);

    public async Task<NoticeFeedDto> GetNoticeFeedAsync(string status, int skip, int take, DateTime? asOf, CancellationToken ct)
    {
        if (status is not ("overdue" or "evictable"))
            throw new DomainRuleException("Choose overdue or eviction notices.", "INVALID_NOTICE_STATUS");
        skip = Math.Clamp(skip, 0, 1_000_000);
        take = Math.Clamp(take, 1, 100);
        var cutoff = asOf ?? DateTime.UtcNow;
        var rows = await snapshots.GetNoticePageAsync(status, cutoff, skip, take + 1, ct);
        var page = rows.Take(take).ToList();
        await RefreshRecordMappingsAsync(page.Select(item => item.Record).ToList(), ct);
        return new(cutoff,
            page.Select(item => new NoticeFeedItemDto(item.SnapshotId, item.SyncedAt,
                MapRecord(item.Record, item.SyncedAt))).ToList(), rows.Count > take, skip + page.Count);
    }

    public async Task<NoticeFeedSummaryDto> GetNoticeSummaryAsync(string status, DateTime dayStart, DateTime dayEnd, CancellationToken ct)
    {
        if (status is not ("overdue" or "evictable"))
            throw new DomainRuleException("Choose overdue or eviction notices.", "INVALID_NOTICE_STATUS");
        if (dayEnd <= dayStart || dayEnd - dayStart > TimeSpan.FromHours(26))
            throw new DomainRuleException("The notice day must be at most 26 hours.", "INVALID_NOTICE_DAY");
        var counts = await snapshots.GetNoticeCountsAsync(status, dayStart, dayEnd, ct);
        return new(counts.FirstOrDefault()?.SyncedAt, counts.FirstOrDefault()?.Current ?? 0,
            counts.Sum(x => x.Total), counts.Sum(x => x.Pending), counts.Sum(x => x.SentToday));
    }

    public async Task<RentRecordPageDto> GetCurrentRecordsAsync(string status, int skip, int take, CancellationToken ct)
    {
        if (status is not ("paid" or "overdue" or "evictable" or "empty"))
            throw new DomainRuleException("Choose a valid rent status.", "INVALID_RENT_STATUS");
        skip = Math.Clamp(skip, 0, 1_000_000);
        take = Math.Clamp(take, 1, 100);
        var page = await snapshots.GetCurrentRecordsAsync(status, skip, take, ct);
        if (page is null) return new(Map(null), false, 0);
        await RefreshTenantMappingsAsync([page.Snapshot], ct);
        var dto = Map(page.Snapshot) with { Total = page.Total, Active = page.Active, Overdue = page.Overdue,
            Evictable = page.Evictable, Empty = page.Empty };
        var next = skip + page.Snapshot.Records.Count;
        return new(dto, next < page.Matching, next);
    }

    public async Task<IReadOnlyList<EvictionQueueItemDto>> GetEvictionQueueAsync(CancellationToken ct)
    {
        var currentSnapshot = (await snapshots.GetCurrentRecordsAsync("evictable", 0, int.MaxValue, ct))?.Snapshot;
        if (currentSnapshot is null) return [];

        var candidates = currentSnapshot.Records.Where(record => record.Status == "evictable").ToList();
        if (candidates.Count == 0) return [];
        var hasLegacyRows = candidates.Any(record => string.IsNullOrWhiteSpace(record.PropertyId));
        var propertyIds = candidates.Where(record => !string.IsNullOrWhiteSpace(record.PropertyId))
            .Select(record => record.PropertyId!).Distinct().ToArray();
        var propertyValues = await properties.GetEvictionCandidatesAsync(hasLegacyRows ? [] : propertyIds, ct);
        var byId = propertyValues.ToDictionary(value => value.Id);
        var byAddress = propertyValues.ToLookup(value => NormalizeAddress(value.PropertyName));
        var remaining = new List<(RentSyncRecord Current, EvictionPropertyLookup Property, string Address)>();
        foreach (var current in candidates)
        {
            var property = !string.IsNullOrWhiteSpace(current.PropertyId)
                ? byId.GetValueOrDefault(current.PropertyId)
                : byAddress[NormalizeAddress(current.Address)].FirstOrDefault();
            if (property is null || property.Status != PropertyStatus.Evictable || property.CurrentTenantId is null)
                continue;

            remaining.Add((current, property, NormalizeAddress(current.Address)));
        }
        if (remaining.Count == 0) return [];

        var sent = new List<(RentSyncRecord Current, EvictionPropertyLookup Property, string SnapshotId, RentSyncRecord Notice)>();
        await foreach (var entry in snapshots.ReadEvictionHistoryAsync(
            currentSnapshot.UpdatedAt, hasLegacyRows ? [] : remaining.Select(x => x.Property.Id).Distinct().ToArray(), ct))
        {
            var recordsById = entry.Records.Where(record => !string.IsNullOrWhiteSpace(record.PropertyId)).ToLookup(record => record.PropertyId!);
            var recordsByAddress = entry.Records.ToLookup(record => NormalizeAddress(record.Address));
            foreach (var candidate in remaining.ToArray())
            {
                var matchingRecord = !string.IsNullOrWhiteSpace(candidate.Current.PropertyId)
                    ? recordsById[candidate.Current.PropertyId].FirstOrDefault() ??
                        recordsByAddress[candidate.Address]
                            .FirstOrDefault(record => string.IsNullOrWhiteSpace(record.PropertyId))
                    : recordsByAddress[candidate.Address].FirstOrDefault();
                if (matchingRecord is null) continue;
                if (matchingRecord.Status == "evictable" && matchingRecord.NoticeGenerated == false) continue;
                // A paid/overdue/empty boundary or the first generated notice settles this
                // property's current cycle. Older rows must never supply a notice or hold.
                remaining.Remove(candidate);
                if (matchingRecord.Status == "evictable" && matchingRecord is { IsResolved: true, ResolvedAt: not null })
                    sent.Add((candidate.Current, candidate.Property, entry.Id, matchingRecord));
            }
            if (remaining.Count == 0) break;
        }
        await RefreshRecordMappingsAsync(sent.Select(item => item.Current).ToList(), ct);
        var now = DateTime.UtcNow;
        var result = new List<EvictionQueueItemDto>();
        foreach (var (current, property, snapshotId, notice) in sent)
        {
            var eligibleAt = notice.ResolvedAt!.Value.AddHours(24);
            result.Add(new EvictionQueueItemDto(
                property.Id,
                property.PropertyId,
                property.PropertyName,
                snapshotId,
                notice.RowNumber,
                current.RenterName,
                current.Cid,
                current.Phone,
                current.DiscordId,
                current.Income,
                notice.ResolvedAt.Value,
                eligibleAt,
                eligibleAt <= now && !notice.IsEvictionOnHold,
                notice.IsEvictionOnHold,
                notice.EvictionHeldByUserId,
                notice.EvictionHeldByDisplayName,
                notice.EvictionHeldAt));
        }

        return result.OrderBy(item => item.EligibleAt).ToList();
    }

    public async Task DeleteAsync(string id, string actorId, CancellationToken ct)
    {
        await snapshots.DeleteAsync(id, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = "rent-data.snapshot-deleted",
            EntityType = "rentSyncSnapshot",
            EntityId = id,
            PerformedByUserId = actorId,
            Metadata = new Dictionary<string, object?>
            {
                ["snapshotId"] = id,
            },
        }, ct);
    }

    public async Task<RentSyncSnapshotDto> SetResolutionAsync(string snapshotId, int rowNumber, bool isResolved, string actorId, CancellationToken ct)
    {
        var (snapshot, _) = await UpdateResolutionAsync(snapshotId, rowNumber, isResolved, actorId, ct);
        await RefreshTenantMappingsAsync([snapshot], ct);
        return Map(snapshot);
    }

    public async Task<RentSyncRecordDto> SetResolutionRecordAsync(string snapshotId, int rowNumber, bool isResolved, string actorId, CancellationToken ct)
    {
        var (snapshot, record) = await UpdateResolutionAsync(snapshotId, rowNumber, isResolved, actorId, ct);
        await RefreshRecordMappingsAsync([record], ct);
        return MapRecord(record, snapshot.UpdatedAt);
    }

    private async Task<(RentSyncSnapshot Snapshot, RentSyncRecord Record)> UpdateResolutionAsync(
        string snapshotId, int rowNumber, bool isResolved, string actorId, CancellationToken ct)
    {
        var snapshot = await snapshots.GetByIdAsync(snapshotId, ct) ?? throw new KeyNotFoundException("Rent sync snapshot not found.");
        var record = snapshot.Records.FirstOrDefault(x => x.RowNumber == rowNumber)
            ?? throw new KeyNotFoundException("Notice record not found.");
        if (record.Status is not ("overdue" or "evictable"))
            throw new DomainRuleException("Only overdue and eviction notices can be resolved.", "NOTICE_NOT_RESOLVABLE");

        if (isResolved)
        {
            var actor = await users.GetByIdAsync(actorId, ct) ?? throw new UnauthorizedAccessException();
            record.IsResolved = true;
            record.ResolvedByUserId = actorId;
            record.ResolvedByDisplayName = actor.DisplayName;
            record.ResolvedAt = DateTime.UtcNow;
        }
        else
        {
            record.IsResolved = false;
            record.ResolvedByUserId = null;
            record.ResolvedByDisplayName = null;
            record.ResolvedAt = null;
            record.IsEvictionOnHold = false;
            record.EvictionHeldByUserId = null;
            record.EvictionHeldByDisplayName = null;
            record.EvictionHeldAt = null;
        }

        snapshot.UpdatedBy = actorId;
        await snapshots.UpdateAsync(snapshot, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = isResolved ? "notice.resolved" : "notice.reopened",
            EntityType = "rentSyncRecord",
            EntityId = $"{snapshotId}:{rowNumber}",
            PerformedByUserId = actorId,
            Metadata = new Dictionary<string, object?> { ["status"] = record.Status, ["address"] = record.Address },
        }, ct);
        return (snapshot, record);
    }

    public async Task SetEvictionHoldAsync(
        string snapshotId, int rowNumber, bool isOnHold, string actorId, CancellationToken ct)
    {
        var snapshot = await snapshots.GetByIdAsync(snapshotId, ct)
            ?? throw new KeyNotFoundException("Rent sync snapshot not found.");
        var record = snapshot.Records.FirstOrDefault(value => value.RowNumber == rowNumber)
            ?? throw new KeyNotFoundException("Eviction notice record not found.");
        if (record.Status != "evictable" || record.NoticeGenerated == false || !record.IsResolved)
            throw new DomainRuleException(
                "Only sent eviction notices can be placed on hold.",
                "EVICTION_NOTICE_NOT_ELIGIBLE");

        if (isOnHold)
        {
            var actor = await users.GetByIdAsync(actorId, ct) ?? throw new UnauthorizedAccessException();
            record.IsEvictionOnHold = true;
            record.EvictionHeldByUserId = actorId;
            record.EvictionHeldByDisplayName = actor.DisplayName;
            record.EvictionHeldAt = DateTime.UtcNow;
        }
        else
        {
            record.IsEvictionOnHold = false;
            record.EvictionHeldByUserId = null;
            record.EvictionHeldByDisplayName = null;
            record.EvictionHeldAt = null;
        }

        snapshot.UpdatedBy = actorId;
        await snapshots.UpdateAsync(snapshot, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = isOnHold ? "eviction.queue.held" : "eviction.queue.released",
            EntityType = "rentSyncRecord",
            EntityId = $"{snapshotId}:{rowNumber}",
            PerformedByUserId = actorId,
            Metadata = new Dictionary<string, object?>
            {
                ["address"] = record.Address,
                ["cid"] = record.Cid,
            },
        }, ct);
    }

    public async Task<RentSyncSnapshotDto> SyncAsync(RentSyncRequest request, string actorId, CancellationToken ct)
    {
        var records = Parse(request.RawData);
        var previousSnapshotTask = snapshots.GetCurrentAsync(ct);
        var cids = records.Where(x => x.Cid.HasValue).Select(x => x.Cid!.Value).Distinct().ToArray();
        var tenantsTask = tenants.GetByCidsAsync(cids, ct);
        var propertiesTask = properties.GetAllAsync(ct);
        await Task.WhenAll(previousSnapshotTask, tenantsTask, propertiesTask);
        var previousSnapshot = previousSnapshotTask.Result;
        var matchedTenants = tenantsTask.Result.ToList();
        var propertyByAddress = propertiesTask.Result
            .GroupBy(property => NormalizeAddress(property.PropertyName))
            .ToDictionary(group => group.Key, group => group.OrderByDescending(x => x.UpdatedAt).First());

        foreach (var record in records)
        {
            propertyByAddress.TryGetValue(NormalizeAddress(record.Address), out var property);
            if (property is null)
            {
                MapKnownTenant(record, matchedTenants);
                continue;
            }

            record.PropertyId = property.Id;
            if (property.Status == PropertyStatus.OnHold)
            {
                // On Hold is a manual administrative status. Rent data must not release
                // the hold or create/update a tenancy until a manager releases it.
                MapKnownTenant(record, matchedTenants);
                continue;
            }
            if (record.Status == "empty")
            {
                await ApplyPropertyStatusAsync(property, record, actorId, ct);
                continue;
            }

            if (!record.Cid.HasValue || string.IsNullOrWhiteSpace(record.RenterName) || string.IsNullOrWhiteSpace(record.Phone))
                continue;

            var tenant = await FindTenantForPropertyAsync(property, record.Cid.Value, matchedTenants, ct);
            if (tenant is null)
            {
                tenant = await ImportTenantAsync(property, record, matchedTenants, actorId, ct);
                matchedTenants.Add(tenant);
                continue;
            }

            record.TenantId = tenant.Id;
            record.DiscordId = tenant.DiscordId;
            var propertyChanged = UpdatePropertyFromRecord(property, tenant, record);
            await ApplyPropertyStatusAsync(property, record, actorId, ct, propertyChanged);
            var tenantChanged = UpdateTenantFromRecord(tenant, record, actorId);
            if (tenantChanged) await tenants.UpdateAsync(tenant, ct);
        }

        MarkNoticeTransitions(records, previousSnapshot?.Records ?? []);

        var snapshot = new RentSyncSnapshot
        {
            Records = records,
            CreatedBy = actorId,
            UpdatedBy = actorId,
            GoogleSheetSyncStatus = googleSheets.IsConfigured ? "pending" : "notConfigured",
            GoogleSheetUrl = googleSheets.SpreadsheetUrl,
        };
        await snapshots.SaveCurrentAsync(snapshot, ct);
        await PublishToGoogleSheetAsync(snapshot, actorId, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = "rent-data.synced",
            EntityType = "rentSyncSnapshot",
            EntityId = snapshot.Id,
            PerformedByUserId = actorId,
            Metadata = new Dictionary<string, object?>
            {
                ["total"] = records.Count,
                ["overdue"] = records.Count(x => x.Status == "overdue"),
                ["evictable"] = records.Count(x => x.Status == "evictable"),
                ["overdueNoticesGenerated"] = records.Count(x => x.Status == "overdue" && x.NoticeGenerated == true),
                ["evictionNoticesGenerated"] = records.Count(x => x.Status == "evictable" && x.NoticeGenerated == true),
                ["unmappedTenants"] = records.Count(x => x.Cid.HasValue && string.IsNullOrWhiteSpace(x.DiscordId)),
                ["googleSheetSyncStatus"] = snapshot.GoogleSheetSyncStatus,
            },
        }, ct);
        return Map(snapshot);
    }

    public async Task<RentSyncSnapshotDto> RetryGoogleSheetSyncAsync(string actorId, CancellationToken ct)
    {
        var snapshot = await snapshots.GetCurrentAsync(ct) ?? throw new KeyNotFoundException("Rent sync snapshot not found.");
        snapshot.GoogleSheetSyncStatus = googleSheets.IsConfigured ? "pending" : "notConfigured";
        snapshot.GoogleSheetSyncError = null;
        snapshot.GoogleSheetUrl = googleSheets.SpreadsheetUrl;
        snapshot.UpdatedBy = actorId;
        await snapshots.UpdateAsync(snapshot, ct);
        await PublishToGoogleSheetAsync(snapshot, actorId, ct);
        return Map(snapshot);
    }

    private async Task PublishToGoogleSheetAsync(RentSyncSnapshot snapshot, string actorId, CancellationToken ct)
    {
        if (!googleSheets.IsConfigured)
        {
            snapshot.GoogleSheetSyncStatus = "notConfigured";
            snapshot.GoogleSheetSyncError = null;
            return;
        }

        try
        {
            await googleSheets.PublishAsync(snapshot.Records, ct);
            snapshot.GoogleSheetSyncStatus = "synced";
            snapshot.GoogleSheetSyncedAt = DateTime.UtcNow;
            snapshot.GoogleSheetSyncError = null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            snapshot.GoogleSheetSyncStatus = "failed";
            snapshot.GoogleSheetSyncError = exception.Message;
        }

        snapshot.UpdatedBy = actorId;
        await snapshots.UpdateAsync(snapshot, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = snapshot.GoogleSheetSyncStatus == "synced" ? "google-sheet.synced" : "google-sheet.sync-failed",
            EntityType = "rentSyncSnapshot",
            EntityId = snapshot.Id,
            PerformedByUserId = actorId,
            Metadata = new Dictionary<string, object?>
            {
                ["status"] = snapshot.GoogleSheetSyncStatus,
                ["syncedAt"] = snapshot.GoogleSheetSyncedAt,
                ["error"] = snapshot.GoogleSheetSyncError,
            },
        }, ct);
    }

    private async Task ApplyPropertyStatusAsync(
        Property property,
        RentSyncRecord record,
        string actorId,
        CancellationToken ct,
        bool forceUpdate = false)
    {
        if (property.Status == PropertyStatus.OnHold) return;

        var previous = property.Status;
        if (record.Status == "empty")
        {
            if (property.CurrentTenantId is not null) return;
            property.MakeAvailable();
        }
        else
        {
            var target = TargetStatus(property, record);
            property.ApplyRentStatus(target);
            if (target == PropertyStatus.Evictable) record.Status = "evictable";
        }

        if (property.Status == previous)
        {
            if (forceUpdate)
            {
                property.UpdatedBy = actorId;
                await properties.UpdateAsync(property, ct);
            }
            return;
        }
        property.UpdatedBy = actorId;
        await properties.UpdateAsync(property, ct);
        await history.CreateAsync(new PropertyStatusHistory
        {
            PropertyId = property.Id,
            PreviousStatus = previous,
            NewStatus = property.Status,
            Reason = "Rent data sync",
            ChangedByUserId = actorId,
            CreatedBy = actorId
        }, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = "property.status.synced",
            EntityType = "property",
            EntityId = property.Id,
            PerformedByUserId = actorId,
            Metadata = new Dictionary<string, object?>
            {
                ["from"] = previous.ToString(),
                ["to"] = property.Status.ToString(),
                ["address"] = record.Address
            }
        }, ct);
    }

    private async Task<Tenant?> FindTenantForPropertyAsync(
        Property property,
        int cid,
        IReadOnlyList<Tenant> matchedTenants,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(property.CurrentTenantId))
        {
            var current = matchedTenants.FirstOrDefault(tenant => tenant.Id == property.CurrentTenantId)
                ?? await tenants.GetByIdAsync(property.CurrentTenantId, ct);
            if (current is null)
                throw new DomainRuleException(
                    $"{property.PropertyName} references a tenant record that no longer exists.",
                    "RENT_SYNC_TENANT_MISSING");
            if (current.Cid != cid)
                throw new DomainRuleException(
                    $"{property.PropertyName} is assigned to CID {current.Cid}, but the export contains CID {cid}.",
                    "RENT_SYNC_TENANT_CONFLICT");
            return current;
        }

        return matchedTenants
            .Where(tenant => tenant.Cid == cid && tenant.PropertyId == property.Id && tenant.Status == TenantStatus.Active)
            .OrderByDescending(tenant => tenant.CreatedAt)
            .FirstOrDefault();
    }

    private async Task<Tenant> ImportTenantAsync(
        Property property,
        RentSyncRecord record,
        IReadOnlyList<Tenant> matchedTenants,
        string actorId,
        CancellationToken ct)
    {
        var previous = property.Status;
        var tenant = new Tenant
        {
            Id = Guid.NewGuid().ToString("N"),
            PropertyId = property.Id,
            FullName = record.RenterName!,
            PhoneNumber = record.Phone!,
            Cid = record.Cid,
            DiscordId = KnownDiscordId(record.Cid!.Value, matchedTenants),
            StartDate = DateTime.UtcNow.Date,
            MonthlyRent = record.Income,
            SecurityDeposit = property.SecurityDeposit ?? 0,
            RentPaidThrough = record.PaidThrough,
            RentalStatus = record.Status,
            Notes = "Imported from rent data sync. Verify Discord ID and security deposit.",
            UpdatedBy = actorId,
        };

        property.Rent = record.Income;
        property.SetTenantForPersistence(tenant.Id);
        property.SetBookingForPersistence(null);
        property.SetUnavailableReasonForPersistence(null);
        property.SetStatusForPersistence(TargetStatus(property, record));
        property.UpdatedBy = actorId;
        record.TenantId = tenant.Id;
        record.DiscordId = tenant.DiscordId;
        if (property.Status == PropertyStatus.Evictable) record.Status = "evictable";
        tenant.RentalStatus = record.Status;

        await lifecycle.AssignTenantAsync(
            property,
            tenant,
            new PropertyStatusHistory
            {
                PropertyId = property.Id,
                PreviousStatus = previous,
                NewStatus = property.Status,
                Reason = "Tenant imported from rent data sync",
                ChangedByUserId = actorId,
                CreatedBy = actorId,
            },
            new AuditLog
            {
                Action = "tenant.imported-from-rent-sync",
                EntityType = "property",
                EntityId = property.Id,
                PerformedByUserId = actorId,
                Metadata = new Dictionary<string, object?>
                {
                    ["tenantId"] = tenant.Id,
                    ["cid"] = tenant.Cid,
                    ["address"] = record.Address,
                },
            },
            null,
            ct);
        return tenant;
    }

    private static bool UpdatePropertyFromRecord(Property property, Tenant tenant, RentSyncRecord record)
    {
        var changed = false;
        if (property.CurrentTenantId != tenant.Id)
        {
            property.SetTenantForPersistence(tenant.Id);
            property.SetBookingForPersistence(null);
            property.SetUnavailableReasonForPersistence(null);
            changed = true;
        }
        if (property.Rent != record.Income)
        {
            property.Rent = record.Income;
            changed = true;
        }
        return changed;
    }

    private static bool UpdateTenantFromRecord(Tenant tenant, RentSyncRecord record, string actorId)
    {
        var changed = false;
        if (!string.Equals(tenant.FullName, record.RenterName, StringComparison.Ordinal))
        {
            tenant.FullName = record.RenterName!;
            changed = true;
        }
        if (!string.Equals(tenant.PhoneNumber, record.Phone, StringComparison.Ordinal))
        {
            tenant.PhoneNumber = record.Phone!;
            changed = true;
        }
        if (tenant.MonthlyRent != record.Income)
        {
            tenant.MonthlyRent = record.Income;
            changed = true;
        }
        if (tenant.RentPaidThrough != record.PaidThrough)
        {
            tenant.RentPaidThrough = record.PaidThrough;
            changed = true;
        }
        if (!string.Equals(tenant.RentalStatus, record.Status, StringComparison.OrdinalIgnoreCase))
        {
            tenant.RentalStatus = record.Status;
            changed = true;
        }
        if (changed)
        {
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = actorId;
        }
        return changed;
    }

    private static void MapKnownTenant(RentSyncRecord record, IReadOnlyList<Tenant> matchedTenants)
    {
        if (!record.Cid.HasValue) return;
        var tenant = matchedTenants
            .Where(value => value.Cid == record.Cid)
            .OrderByDescending(value => value.Status == TenantStatus.Active)
            .ThenByDescending(value => value.CreatedAt)
            .FirstOrDefault();
        if (tenant is null) return;
        record.TenantId = tenant.Id;
        record.DiscordId = tenant.DiscordId;
    }

    private static string KnownDiscordId(int cid, IReadOnlyList<Tenant> matchedTenants) =>
        matchedTenants
            .Where(tenant => tenant.Cid == cid && !string.IsNullOrWhiteSpace(tenant.DiscordId))
            .OrderByDescending(tenant => tenant.Status == TenantStatus.Active)
            .ThenByDescending(tenant => tenant.CreatedAt)
            .Select(tenant => tenant.DiscordId)
            .FirstOrDefault() ?? string.Empty;

    private static PropertyStatus TargetStatus(Property property, RentSyncRecord record) => record.Status switch
    {
        "paid" => PropertyStatus.Paid,
        "evictable" => PropertyStatus.Evictable,
        "overdue" when property.Status == PropertyStatus.Overdue &&
            property.StatusChangedAt <= DateTime.UtcNow.AddDays(-7) => PropertyStatus.Evictable,
        "overdue" => PropertyStatus.Overdue,
        _ => property.Status,
    };

    private static List<RentSyncRecord> Parse(string? rawData)
    {
        if (string.IsNullOrWhiteSpace(rawData))
            throw new DomainRuleException("Paste exported property data before syncing.", "RENT_SYNC_EMPTY");

        var lines = rawData.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var header = NormalizeHeader(lines[0]);
        var hasPaymentDates = header.SequenceEqual(ExpectedHeader.Concat(["last paid", "next charge"]));
        if (lines.Length < 2 || (!hasPaymentDates && !header.SequenceEqual(ExpectedHeader)))
            throw new DomainRuleException("The export header must be Status, Address, Interior, Renter CID, Renter Name, Phone, Income, Cost, optionally followed by Last Paid, Next Charge.", "RENT_SYNC_HEADER_INVALID");

        var result = new List<RentSyncRecord>();
        for (var index = 1; index < lines.Length; index++)
        {
            var match = RowPattern.Match(lines[index]);
            if (!match.Success || match.Groups["lastPaid"].Success != hasPaymentDates)
                throw new DomainRuleException($"Row {index + 1} does not match the expected export format.", "RENT_SYNC_ROW_INVALID");

            var (status, paidThrough) = ParseStatus(match.Groups["status"].Value, index + 1);
            result.Add(new RentSyncRecord
            {
                RowNumber = index,
                Status = status,
                PaidThrough = paidThrough,
                LastPaid = hasPaymentDates ? ParsePaymentDate(match.Groups["lastPaid"].Value, "Last Paid", index + 1) : null,
                NextCharge = hasPaymentDates ? ParsePaymentDate(match.Groups["nextCharge"].Value, "Next Charge", index + 1) : null,
                Address = match.Groups["address"].Value.Trim(),
                Interior = match.Groups["interior"].Value.Trim(),
                Cid = ParseCid(match.Groups["cid"].Value, index + 1),
                RenterName = Optional(match.Groups["name"].Value),
                Phone = ParsePhone(match.Groups["phone"].Value, index + 1),
                Income = ParseCurrency(match.Groups["income"].Value, index + 1),
                Cost = ParseCurrency(match.Groups["cost"].Value, index + 1),
            });
        }

        if (result.Count == 0)
            throw new DomainRuleException("The export does not contain any property rows.", "RENT_SYNC_NO_ROWS");
        return result;
    }

    private static readonly string[] ExpectedHeader =
        ["status", "address", "interior", "renter cid", "renter name", "phone", "income", "cost"];

    private static string[] NormalizeHeader(string header) => header.TrimStart('\uFEFF').Split(',')
        .Select(value => value.Trim().ToLowerInvariant()).ToArray();

    private static (string Status, DateTime? PaidThrough) ParseStatus(string value, int row)
    {
        var status = value.Trim();
        if (status.Equals("Paid", StringComparison.OrdinalIgnoreCase)) return ("paid", null);
        if (status.StartsWith("Paid ", StringComparison.OrdinalIgnoreCase))
        {
            if (!DateTime.TryParse(status[5..], CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date))
                throw new DomainRuleException($"Row {row} contains an invalid paid-through date.", "RENT_SYNC_STATUS_INVALID");
            return ("paid", date.Date);
        }

        if (status.Equals("Overdue", StringComparison.OrdinalIgnoreCase)) return ("overdue", null);
        if (status.Equals("Evictable", StringComparison.OrdinalIgnoreCase)) return ("evictable", null);
        if (status.Equals("Empty", StringComparison.OrdinalIgnoreCase)) return ("empty", null);
        throw new DomainRuleException($"Row {row} has an unsupported status '{status}'.", "RENT_SYNC_STATUS_INVALID");
    }

    private static DateTime? ParsePaymentDate(string value, string column, int row)
    {
        value = value.Trim();
        if (value.Length == 0 || value.Equals("N/A", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) return null;
        if (DateTime.TryParseExact(value, ["M/d/yyyy", "MM/dd/yyyy", "M/dd/yyyy", "MM/d/yyyy", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
        throw new DomainRuleException($"Row {row} contains an invalid {column} date. Use M/d/yyyy, Unknown or N/A.", "RENT_SYNC_DATE_INVALID");
    }

    private static int? ParseCid(string value, int row)
    {
        if (value.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase)) return null;
        if (int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var cid) && cid > 0) return cid;
        throw new DomainRuleException($"Row {row} contains an invalid renter CID.", "RENT_SYNC_CID_INVALID");
    }

    private static string? ParsePhone(string value, int row)
    {
        var phone = Optional(value);
        if (phone is null || Regex.IsMatch(phone, @"^\d{3}-\d{4}$", RegexOptions.CultureInvariant))
            return phone;
        throw new DomainRuleException(
            $"Row {row} contains an invalid phone number. Use the format 123-4567.",
            "RENT_SYNC_PHONE_INVALID");
    }

    private static decimal ParseCurrency(string value, int row)
    {
        var normalized = value.Replace("$", string.Empty, StringComparison.Ordinal).Replace(",", string.Empty, StringComparison.Ordinal);
        if (decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) && amount >= 0) return amount;
        throw new DomainRuleException($"Row {row} contains an invalid currency value.", "RENT_SYNC_CURRENCY_INVALID");
    }

    private static string? Optional(string value) => value.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();

    private static void MarkNoticeTransitions(
        IReadOnlyList<RentSyncRecord> currentRecords,
        IReadOnlyList<RentSyncRecord> previousRecords)
    {
        foreach (var current in currentRecords)
        {
            var previous = FindPreviousRecord(current, previousRecords);
            current.NoticeGenerated = current.Status switch
            {
                "overdue" => previous is null || previous.Status is "paid" or "evictable",
                "evictable" => previous is null || previous.Status is "paid" or "overdue",
                _ => false,
            };
        }
    }

    private static RentSyncRecord? FindPreviousRecord(
        RentSyncRecord current,
        IReadOnlyList<RentSyncRecord> previousRecords)
    {
        if (!string.IsNullOrWhiteSpace(current.PropertyId))
        {
            var propertyMatch = previousRecords.FirstOrDefault(previous =>
                string.Equals(previous.PropertyId, current.PropertyId, StringComparison.Ordinal));
            if (propertyMatch is not null) return propertyMatch;
        }

        var normalizedAddress = NormalizeAddress(current.Address);
        return previousRecords.FirstOrDefault(previous =>
            NormalizeAddress(previous.Address) == normalizedAddress);
    }

    private static string NormalizeAddress(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();

    private async Task RefreshTenantMappingsAsync(
        IReadOnlyList<RentSyncSnapshot> snapshotValues,
        CancellationToken ct) =>
        await RefreshRecordMappingsAsync(snapshotValues.SelectMany(value => value.Records).ToList(), ct);

    private async Task RefreshRecordMappingsAsync(IReadOnlyList<RentSyncRecord> values, CancellationToken ct)
    {
        var records = values.Where(value => value.Cid.HasValue).ToList();
        if (records.Count == 0) return;

        var matchedTenants = await tenants.GetNoticeMappingsAsync(
            records.Select(value => value.Cid!.Value).Distinct().ToArray(), ct);
        var tenantsByCid = matchedTenants.Where(tenant => !string.IsNullOrWhiteSpace(tenant.DiscordId))
            .ToLookup(tenant => tenant.Cid);
        foreach (var record in records)
        {
            var mappedTenant = tenantsByCid[record.Cid]
                .OrderByDescending(tenant => tenant.Id == record.TenantId)
                .ThenByDescending(tenant => tenant.Status == TenantStatus.Active)
                .ThenByDescending(tenant => tenant.UpdatedAt)
                .FirstOrDefault();
            if (mappedTenant is null) continue;

            record.TenantId ??= mappedTenant.Id;
            record.DiscordId = mappedTenant.DiscordId.Trim();
        }
    }

    private RentSyncSnapshotDto Map(RentSyncSnapshot? snapshot)
    {
        if (snapshot is null) return new(string.Empty, null, null, 0, 0, 0, 0, 0, 0,
            googleSheets.IsConfigured ? "ready" : "notConfigured", null, null, googleSheets.SpreadsheetUrl, Array.Empty<RentSyncRecordDto>());
        var records = snapshot.Records.Select(record => MapRecord(record, snapshot.UpdatedAt)).ToList();
        return new(
            snapshot.Id,
            snapshot.CreatedBy,
            snapshot.UpdatedAt,
            records.Count,
            records.Count(x => x.Status == "paid"),
            records.Count(x => x.Status == "overdue"),
            records.Count(x => x.Status == "evictable"),
            records.Count(x => x.Status == "empty"),
            records.Count(x => x.Cid.HasValue && string.IsNullOrWhiteSpace(x.DiscordId)),
            snapshot.GoogleSheetSyncStatus == "notConfigured" && googleSheets.IsConfigured
                ? "ready"
                : snapshot.GoogleSheetSyncStatus,
            snapshot.GoogleSheetSyncedAt,
            snapshot.GoogleSheetSyncError,
            snapshot.GoogleSheetUrl,
            records);
    }

    private static RentSyncRecordDto MapRecord(RentSyncRecord record, DateTime syncedAt) => new(
        record.RowNumber, record.Status, record.PaidThrough, record.Address, record.Interior,
        record.Cid, record.RenterName, record.Phone, record.Income, record.Cost,
        record.TenantId, record.DiscordId, !string.IsNullOrWhiteSpace(record.TenantId),
        record.Status == "overdue" && record.NoticeGenerated != false ? OverdueNotice(record, syncedAt.Date.AddDays(7)) : null,
        record.Status == "evictable" && record.NoticeGenerated != false ? EvictionNotice(record) : null,
        record.IsResolved, record.ResolvedByUserId, record.ResolvedByDisplayName, record.ResolvedAt,
        record.LastPaid, record.NextCharge);

    private static string OverdueNotice(RentSyncRecord record, DateTime evictionDate) =>
        $"⚠️ Overdue Payment Notice\n\nNOTICE OF PAYMENT AND EVICTION: {record.RenterName}, this is to inform you that your house {record.Address} is overdue due to unpaid dues. You have 1 Week to pay the rent to avoid eviction. Thank You State Real Estate. Date of Eviction: {evictionDate:dd MMMM yyyy}";

    private static string EvictionNotice(RentSyncRecord record) =>
        $"Name : {record.RenterName}\nCID  : {record.Cid}\nRent : {record.Income.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}\nAddress : {record.Address}\nStatus : Evictable\nNote : If you do not clear your dues in 24 Hours you will be evicted\nNotify : {(string.IsNullOrWhiteSpace(record.DiscordId) ? $"Discord ID unavailable for CID {record.Cid}" : $"<@{record.DiscordId}>")}";
}
