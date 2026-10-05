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
        @"^(?<status>[^,]+),(?<address>[^,]+),(?<interior>[^,]+),(?<cid>[^,]+),(?<name>[^,]+),(?<phone>[^,]+),(?<income>\$[\d,]+),(?<cost>\$[\d,]+)$",
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

    public async Task<IReadOnlyList<EvictionQueueItemDto>> GetEvictionQueueAsync(CancellationToken ct)
    {
        var allSnapshots = await snapshots.GetAllAsync(ct);
        await RefreshTenantMappingsAsync(allSnapshots, ct);
        var currentSnapshot = allSnapshots.FirstOrDefault();
        if (currentSnapshot is null) return [];

        var now = DateTime.UtcNow;
        var result = new List<EvictionQueueItemDto>();
        foreach (var current in currentSnapshot.Records.Where(record => record.Status == "evictable"))
        {
            var property = !string.IsNullOrWhiteSpace(current.PropertyId)
                ? await properties.GetByIdAsync(current.PropertyId, ct)
                : await properties.GetByNameAsync(current.Address, ct);
            if (property is null || property.Status != PropertyStatus.Evictable || property.CurrentTenantId is null)
                continue;

            RentSyncSnapshot? noticeSnapshot = null;
            RentSyncRecord? notice = null;
            foreach (var snapshot in allSnapshots)
            {
                var matchingRecord = snapshot.Records.FirstOrDefault(record => IsSameProperty(current, record));
                if (matchingRecord is null) continue;

                // A paid/overdue/empty record closes the previous eviction cycle. Never
                // reuse a sent notice (or its queue hold) from before that boundary.
                if (matchingRecord.Status != "evictable") break;
                if (matchingRecord.NoticeGenerated == false) continue;

                noticeSnapshot = snapshot;
                notice = matchingRecord;
                break;
            }
            if (noticeSnapshot is null || notice is not { IsResolved: true, ResolvedAt: not null }) continue;

            var eligibleAt = notice.ResolvedAt.Value.AddHours(24);
            result.Add(new EvictionQueueItemDto(
                property.Id,
                property.PropertyId,
                property.PropertyName,
                noticeSnapshot.Id,
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
        await RefreshTenantMappingsAsync([snapshot], ct);
        return Map(snapshot);
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
        var previousSnapshot = await snapshots.GetCurrentAsync(ct);
        var cids = records.Where(x => x.Cid.HasValue).Select(x => x.Cid!.Value).Distinct().ToArray();
        var matchedTenants = (await tenants.GetByCidsAsync(cids, ct)).ToList();
        var propertyByAddress = (await properties.GetAllAsync(ct))
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
        if (lines.Length < 2 || !NormalizeHeader(lines[0]).SequenceEqual(ExpectedHeader))
            throw new DomainRuleException("The export header must be Status, Address, Interior, Renter CID, Renter Name, Phone, Income, Cost.", "RENT_SYNC_HEADER_INVALID");

        var result = new List<RentSyncRecord>();
        for (var index = 1; index < lines.Length; index++)
        {
            var match = RowPattern.Match(lines[index]);
            if (!match.Success)
                throw new DomainRuleException($"Row {index + 1} does not match the expected export format.", "RENT_SYNC_ROW_INVALID");

            var (status, paidThrough) = ParseStatus(match.Groups["status"].Value, index + 1);
            result.Add(new RentSyncRecord
            {
                RowNumber = index,
                Status = status,
                PaidThrough = paidThrough,
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

    private static bool IsSameProperty(RentSyncRecord left, RentSyncRecord right)
    {
        if (!string.IsNullOrWhiteSpace(left.PropertyId) && !string.IsNullOrWhiteSpace(right.PropertyId))
            return string.Equals(left.PropertyId, right.PropertyId, StringComparison.Ordinal);
        return NormalizeAddress(left.Address) == NormalizeAddress(right.Address);
    }

    private static string NormalizeAddress(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ").ToUpperInvariant();

    private async Task RefreshTenantMappingsAsync(
        IReadOnlyList<RentSyncSnapshot> snapshotValues,
        CancellationToken ct)
    {
        var records = snapshotValues.SelectMany(value => value.Records).Where(value => value.Cid.HasValue).ToList();
        if (records.Count == 0) return;

        var matchedTenants = await tenants.GetByCidsAsync(
            records.Select(value => value.Cid!.Value).Distinct().ToArray(), ct);
        foreach (var record in records)
        {
            var mappedTenant = matchedTenants
                .Where(tenant =>
                    tenant.Cid == record.Cid &&
                    !string.IsNullOrWhiteSpace(tenant.DiscordId))
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
        var evictionDate = snapshot.UpdatedAt.Date.AddDays(7);
        var records = snapshot.Records.Select(record => new RentSyncRecordDto(
            record.RowNumber, record.Status, record.PaidThrough, record.Address, record.Interior,
            record.Cid, record.RenterName, record.Phone, record.Income, record.Cost,
            record.TenantId, record.DiscordId, !string.IsNullOrWhiteSpace(record.TenantId),
            record.Status == "overdue" && record.NoticeGenerated != false ? OverdueNotice(record, evictionDate) : null,
            record.Status == "evictable" && record.NoticeGenerated != false ? EvictionNotice(record) : null,
            record.IsResolved, record.ResolvedByUserId, record.ResolvedByDisplayName, record.ResolvedAt)).ToList();
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

    private static string OverdueNotice(RentSyncRecord record, DateTime evictionDate) =>
        $"⚠️ Overdue Payment Notice\n\nNOTICE OF PAYMENT AND EVICTION: {record.RenterName}, this is to inform you that your house {record.Address} is overdue due to unpaid dues. You have 1 Week to pay the rent to avoid eviction. Thank You State Real Estate. Date of Eviction: {evictionDate:dd MMMM yyyy}";

    private static string EvictionNotice(RentSyncRecord record) =>
        $"Name : {record.RenterName}\nCID  : {record.Cid}\nRent : {record.Income.ToString("C0", CultureInfo.GetCultureInfo("en-US"))}\nAddress : {record.Address}\nStatus : Evictable\nNote : If you do not clear your dues in 24 Hours you will be evicted\nNotify : {(string.IsNullOrWhiteSpace(record.DiscordId) ? $"Discord ID unavailable for CID {record.Cid}" : $"<@{record.DiscordId}>")}";
}
