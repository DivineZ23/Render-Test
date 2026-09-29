using System.Globalization;
using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;

namespace ImperialEstates.Application.Services;

public sealed class AuditLogService(
    IAuditRepository audits,
    IUserRepository users,
    IPropertyRepository properties,
    IBlockRepository blocks,
    IEnquiryRepository enquiries,
    IRecruitmentApplicationRepository recruitmentApplications,
    ICommissionRepository commissions,
    IRentSyncRepository rentSync)
{
    private static readonly IReadOnlyDictionary<string, ActionPresentation> Actions =
        new Dictionary<string, ActionPresentation>(StringComparer.OrdinalIgnoreCase)
        {
            ["user.approved"] = new("User approved", "access", "success", "Approved"),
            ["user.rejected"] = new("User rejected", "access", "danger", "Rejected"),
            ["user.promoted"] = new("User promoted", "access", "success", "Promoted"),
            ["user.demoted"] = new("User demoted", "access", "warning", "Demoted"),
            ["user.revoked"] = new("Access revoked", "access", "danger", "Revoked access for"),
            ["user.restored"] = new("Access restored", "access", "success", "Restored access for"),
            ["user.deleted"] = new("User deleted", "access", "danger", "Deleted"),
            ["user.profile-updated"] = new("Profile updated", "access", "info", "Updated"),
            ["settings.access_management.updated"] = new("Access rules updated", "access", "warning", "Updated"),
            ["settings.team.updated"] = new("Team settings updated", "access", "info", "Updated"),
            ["recruitment.application.reviewed"] = new("Application reviewed", "access", "info", "Reviewed"),
            ["recruitment.settings.updated"] = new("Recruitment settings updated", "access", "info", "Updated"),
            ["block.created"] = new("Block created", "portfolio", "success", "Created"),
            ["block.updated"] = new("Block updated", "portfolio", "info", "Updated"),
            ["block.deleted"] = new("Block deleted", "portfolio", "danger", "Deleted"),
            ["property.created"] = new("Property created", "portfolio", "success", "Created"),
            ["property.updated"] = new("Property updated", "portfolio", "info", "Updated"),
            ["property.deleted"] = new("Property deleted", "portfolio", "danger", "Deleted"),
            ["property.status.changed"] = new("Property status changed", "portfolio", "warning", "Changed the status of"),
            ["property.status.synced"] = new("Property status synced", "operations", "info", "Synced the status of"),
            ["property.booked"] = new("Property booked", "portfolio", "success", "Booked"),
            ["property.booking.created"] = new("Booking created", "portfolio", "success", "Created a booking for"),
            ["property.booking.cancelled"] = new("Booking cancelled", "portfolio", "warning", "Cancelled a booking for"),
            ["property.booking.released"] = new("Booking released", "portfolio", "warning", "Released a booking for"),
            ["property.bookings.closed-all"] = new("Bookings closed", "portfolio", "warning", "Closed all bookings for"),
            ["property.booking-announcement.posted"] = new("Booking announcement posted", "portfolio", "success", "Posted the booking announcement for"),
            ["property.booking-announcement.reopened"] = new("Booking announcement reopened", "portfolio", "warning", "Reopened the booking announcement for"),
            ["tenant.assigned"] = new("Tenant assigned", "portfolio", "success", "Assigned a tenant to"),
            ["tenant.updated"] = new("Tenant updated", "portfolio", "info", "Updated the tenant for"),
            ["tenant.evicted"] = new("Tenant evicted", "portfolio", "danger", "Evicted the tenant from"),
            ["tenant.imported-from-rent-sync"] = new("Tenant imported", "operations", "success", "Imported a tenant for"),
            ["enquiry.updated"] = new("Enquiry updated", "portfolio", "info", "Updated"),
            ["commission.auction-settlement.created"] = new("Settlement recorded", "finance", "success", "Recorded"),
            ["commission.auction-settlement.updated"] = new("Settlement updated", "finance", "info", "Updated"),
            ["commission.auction-settlement.deleted"] = new("Settlement deleted", "finance", "danger", "Deleted"),
            ["commission.paid"] = new("Commission paid", "finance", "success", "Marked as paid"),
            ["commission.marked-unpaid"] = new("Commission marked unpaid", "finance", "warning", "Marked as unpaid"),
            ["rent-data.synced"] = new("Rent data synced", "operations", "success", "Completed"),
            ["rent-data.snapshot-deleted"] = new("Rent snapshot deleted", "operations", "danger", "Deleted"),
            ["google-sheet.synced"] = new("Google Sheet synced", "operations", "success", "Synced"),
            ["google-sheet.sync-failed"] = new("Google Sheet sync failed", "operations", "danger", "Failed to sync"),
            ["notice.resolved"] = new("Notice resolved", "operations", "success", "Resolved"),
            ["notice.reopened"] = new("Notice reopened", "operations", "warning", "Reopened"),
            ["eviction.queue.held"] = new("Eviction placed on hold", "operations", "warning", "Placed on hold"),
            ["eviction.queue.released"] = new("Eviction hold released", "operations", "info", "Released the hold on")
        };

    public async Task<PagedResult<AuditLogDto>> QueryAsync(AuditLogQuery query, CancellationToken ct)
    {
        var preparedQuery = await EnrichSearchAsync(query, ct);
        var page = await audits.QueryAsync(preparedQuery, ct);
        if (page.Items.Count == 0)
            return new([], page.Page, page.PageSize, page.TotalItems);

        var actorIds = page.Items.Select(x => x.PerformedByUserId)
            .Concat(page.Items.Where(x => x.EntityType == "user").Select(x => x.EntityId))
            .Concat(page.Items.Select(x => Value(x.Metadata, "agentUserId")))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().Distinct().ToArray();
        var propertyIds = EntityIds(page.Items, "property");
        var enquiryIds = EntityIds(page.Items, "enquiry");
        var applicationIds = EntityIds(page.Items, "recruitment_application");
        var commissionIds = EntityIds(page.Items, "commission");
        var snapshotIds = page.Items
            .Where(x => x.EntityType is "rentSyncSnapshot" or "rentSyncRecord")
            .Select(x => x.EntityId.Split(':', 2)[0]).Distinct().ToArray();

        var usersTask = users.GetByIdsAsync(actorIds, ct);
        var propertiesTask = properties.GetByIdsAsync(propertyIds, ct);
        var blocksTask = blocks.GetAllAsync(false, ct);
        var enquiriesTask = FetchAsync(enquiryIds, enquiries.GetByIdAsync, ct);
        var applicationsTask = FetchAsync(applicationIds, recruitmentApplications.GetByIdAsync, ct);
        var commissionsTask = FetchAsync(commissionIds, commissions.GetByIdAsync, ct);
        var snapshotsTask = FetchAsync(snapshotIds, rentSync.GetByIdAsync, ct);
        await Task.WhenAll(usersTask, propertiesTask, blocksTask, enquiriesTask,
            applicationsTask, commissionsTask, snapshotsTask);

        var userMap = usersTask.Result.ToDictionary(x => x.Id);
        var propertyMap = propertiesTask.Result.ToDictionary(x => x.Id);
        var blockMap = blocksTask.Result.ToDictionary(x => x.Id);
        var enquiryMap = enquiriesTask.Result.ToDictionary(x => x.Id);
        var applicationMap = applicationsTask.Result.ToDictionary(x => x.Id);
        var commissionMap = commissionsTask.Result.ToDictionary(x => x.Id);
        var snapshotMap = snapshotsTask.Result.ToDictionary(x => x.Id);

        var items = page.Items.Select(log => Map(log, userMap, propertyMap, blockMap,
            enquiryMap, applicationMap, commissionMap, snapshotMap)).ToList();
        return new(items, page.Page, page.PageSize, page.TotalItems);
    }

    private async Task<AuditLogQuery> EnrichSearchAsync(AuditLogQuery query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query.Search)) return query;
        var search = query.Search.Trim();
        var actorTask = users.SearchIdsAsync(search, ct);
        var propertyTask = properties.QueryAsync(new PropertyQuery
        {
            Search = search,
            Page = 1,
            PageSize = 100
        }, false, ct);
        var blocksTask = blocks.GetAllAsync(false, ct);
        await Task.WhenAll(actorTask, propertyTask, blocksTask);
        var blockIds = blocksTask.Result
            .Where(x => x.BlockName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        x.BlockId.ToString(CultureInfo.InvariantCulture).Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id);
        return new AuditLogQuery
        {
            Page = query.Page,
            PageSize = query.PageSize,
            Search = search,
            Category = query.Category,
            EntityType = query.EntityType,
            ActorId = query.ActorId,
            From = query.From,
            To = query.To,
            SortDirection = query.SortDirection,
            MatchingActorIds = actorTask.Result,
            MatchingEntityIds = propertyTask.Result.Items.Select(x => x.Id).Concat(blockIds).Distinct().ToArray()
        };
    }

    private static AuditLogDto Map(
        AuditLog log,
        IReadOnlyDictionary<string, User> userMap,
        IReadOnlyDictionary<string, Property> propertyMap,
        IReadOnlyDictionary<string, Block> blockMap,
        IReadOnlyDictionary<string, Enquiry> enquiryMap,
        IReadOnlyDictionary<string, RecruitmentApplication> applicationMap,
        IReadOnlyDictionary<string, CommissionRecord> commissionMap,
        IReadOnlyDictionary<string, RentSyncSnapshot> snapshotMap)
    {
        var actor = userMap.GetValueOrDefault(log.PerformedByUserId);
        var presentation = Actions.GetValueOrDefault(log.Action) ?? UnknownAction(log.Action);
        var entityTypeLabel = EntityTypeLabel(log.EntityType);
        var entityName = EntityName(log, entityTypeLabel, userMap, propertyMap, blockMap,
            enquiryMap, applicationMap, commissionMap, snapshotMap);
        return new AuditLogDto(
            log.Id,
            log.Action,
            presentation.Label,
            presentation.Category,
            presentation.Severity,
            $"{presentation.Verb} {entityName}",
            log.EntityType,
            entityTypeLabel,
            log.EntityId,
            entityName,
            log.PerformedByUserId,
            actor?.DisplayName ?? actor?.Username ?? "Former or unknown user",
            actor?.AvatarUrl,
            actor?.Role.ToString(),
            log.PreviousValues,
            log.NewValues,
            log.Metadata,
            log.CreatedAt);
    }

    private static string EntityName(
        AuditLog log,
        string typeLabel,
        IReadOnlyDictionary<string, User> users,
        IReadOnlyDictionary<string, Property> properties,
        IReadOnlyDictionary<string, Block> blocks,
        IReadOnlyDictionary<string, Enquiry> enquiries,
        IReadOnlyDictionary<string, RecruitmentApplication> applications,
        IReadOnlyDictionary<string, CommissionRecord> commissions,
        IReadOnlyDictionary<string, RentSyncSnapshot> snapshots)
    {
        if (log.EntityType == "user" && users.TryGetValue(log.EntityId, out var user))
            return user.DisplayName;
        if (log.EntityType == "property" && properties.TryGetValue(log.EntityId, out var property))
            return $"{property.PropertyName} · Property #{property.PropertyId}";
        if (log.EntityType == "block" && blocks.TryGetValue(log.EntityId, out var block))
            return $"{block.BlockName} · Block #{block.BlockId}";
        if (log.EntityType == "enquiry" && enquiries.TryGetValue(log.EntityId, out var enquiry))
            return $"enquiry from {enquiry.FullName}";
        if (log.EntityType == "recruitment_application" && applications.TryGetValue(log.EntityId, out var application))
            return $"application from {application.CharacterName}";
        if (log.EntityType == "commission" && commissions.TryGetValue(log.EntityId, out var commission))
            return $"{commission.AgentDisplayName}'s commission · {commission.AuctionReference}";
        if (log.EntityType == "commission_settlement")
            return $"auction settlement {Value(log.Metadata, "auctionReference") ?? "record"}";
        if (log.EntityType == "rentSyncRecord")
            return Value(log.Metadata, "address") is { } address ? $"rent record for {address}" : "rent record";
        if (log.EntityType == "rentSyncSnapshot")
        {
            var id = log.EntityId.Split(':', 2)[0];
            if (snapshots.TryGetValue(id, out var snapshot))
                return $"rent sync from {snapshot.CreatedAt.ToLocalTime():dd MMM yyyy, h:mm tt}";
            return "rent sync snapshot";
        }
        if (log.EntityType == "application_setting")
            return log.EntityId switch
            {
                "access.management" => "access management rules",
                "public.team" => "public team settings",
                "recruitment.enabled" => "recruitment availability",
                _ => "application settings"
            };
        return $"{typeLabel.ToLowerInvariant()} record";
    }

    private static string EntityTypeLabel(string value) => value switch
    {
        "rentSyncRecord" => "Rent record",
        "rentSyncSnapshot" => "Rent sync",
        "recruitment_application" => "Recruitment application",
        "commission_settlement" => "Auction settlement",
        "application_setting" => "Settings",
        _ => Humanize(value)
    };

    private static ActionPresentation UnknownAction(string action)
    {
        var category = action.Split('.', 2)[0] switch
        {
            "user" or "settings" or "recruitment" => "access",
            "commission" => "finance",
            "rent-data" or "google-sheet" or "notice" or "eviction" => "operations",
            _ => "portfolio"
        };
        return new(Humanize(action.Replace('.', ' ')), category, "info", "Changed");
    }

    private static string Humanize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Record";
        var text = value.Replace('_', ' ').Replace('-', ' ');
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string? Value(IReadOnlyDictionary<string, object?>? metadata, string key)
    {
        if (metadata is null || !metadata.TryGetValue(key, out var value) || value is null) return null;
        return value is IFormattable formattable
            ? formattable.ToString(null, CultureInfo.InvariantCulture)
            : value.ToString();
    }

    private static string[] EntityIds(IEnumerable<AuditLog> logs, string entityType) =>
        logs.Where(x => x.EntityType == entityType).Select(x => x.EntityId).Distinct().ToArray();

    private static async Task<IReadOnlyList<T>> FetchAsync<T>(
        IEnumerable<string> ids,
        Func<string, CancellationToken, Task<T?>> fetch,
        CancellationToken ct) where T : class
    {
        var values = await Task.WhenAll(ids.Select(id => fetch(id, ct)));
        return values.OfType<T>().ToList();
    }

    private sealed record ActionPresentation(string Label, string Category, string Severity, string Verb);
}
