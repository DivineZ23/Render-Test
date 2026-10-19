using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Application.Services;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Tests.Services;

public sealed class RentSyncServiceTests
{
    [Fact]
    public async Task Ten_column_export_preserves_dates_statuses_and_currency_commas()
    {
        var snapshots = new SnapshotRepository();
        var result = await CreateService(snapshots).SyncAsync(new RentSyncRequest("""
            Status,Address,Interior,Renter CID,Renter Name,Phone,Income,Cost,Last Paid,Next Charge
            Paid,Example House 1,Franklin's Mansion,99,Test Renter,333-4444,$18,000,$8,000,Unknown,10/22/2026
            Paid,Example House 2,Mid-End Apartment (House),99,Test Renter,333-4444,$8,500.50,$4,500.25,10/19/2026,10/24/2026
            Overdue,Example House 3,Franklin's Mansion,99,Test Renter,333-4444,$22,000,$8,000,Unknown,10/18/2026
            Evictable,Example House 4,Trevor's Beach House,99,Test Renter,333-4444,$10,000,$5,000,Unknown,10/8/2026
            Empty,Example House 5,Michael's Mansion,N/A,N/A,N/A,$0,$8,000,N/A,N/A
            """), "owner-1", default);
        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Active);
        Assert.Equal(1, result.Overdue);
        Assert.Equal(1, result.Evictable);
        Assert.Equal(1, result.Empty);
        Assert.Equal(58500.50m, result.Records.Sum(x => x.Income));
        Assert.Equal(33500.25m, result.Records.Sum(x => x.Cost));
        Assert.Null(result.Records[0].LastPaid);
        Assert.Equal(new DateTime(2026, 10, 22), result.Records[0].NextCharge);
        Assert.Equal(new DateTime(2026, 10, 19), result.Records[1].LastPaid);
        Assert.Equal(new DateTime(2026, 10, 24), result.Records[1].NextCharge);
        Assert.Null(result.Records[1].PaidThrough);
        Assert.Null(result.Records[4].LastPaid);
        Assert.Null(result.Records[4].NextCharge);
        Assert.Equal(result.Records[1].LastPaid, (await snapshots.GetCurrentAsync(default))!.Records[1].LastPaid);
        Assert.Equal(result.Records[0].NextCharge, (await CreateService(snapshots).GetCurrentAsync(default)).Records[0].NextCharge);
    }

    [Theory]
    [InlineData("Unknown", "N/A")]
    [InlineData("", "")]
    public async Task Missing_payment_dates_are_optional(string lastPaid, string nextCharge)
    {
        var result = await CreateService(new SnapshotRepository()).SyncAsync(new RentSyncRequest(
            DatedExport("Paid", lastPaid, nextCharge)), "owner-1", default);
        Assert.Null(Assert.Single(result.Records).LastPaid);
        Assert.Null(Assert.Single(result.Records).NextCharge);
    }

    [Theory]
    [InlineData("2/30/2026", "10/22/2026")]
    [InlineData("Unknown", "19/10/2026")]
    [InlineData("yesterday", "10/22/2026")]
    public async Task Invalid_payment_dates_reject_the_entire_export_before_saving(string lastPaid, string nextCharge)
    {
        var snapshots = new SnapshotRepository();
        var service = CreateService(snapshots);
        var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.SyncAsync(
            new RentSyncRequest(DatedExport("Paid", lastPaid, nextCharge)), "owner-1", default));
        Assert.Equal("RENT_SYNC_DATE_INVALID", error.ErrorCode);
        Assert.Contains("Row 2", error.Message);
        Assert.Null(await snapshots.GetCurrentAsync(default));
    }

    [Fact]
    public async Task Header_and_row_column_counts_must_agree()
    {
        var service = CreateService(new SnapshotRepository());
        var legacyWithDates = Export("Paid") + ",Unknown,10/22/2026";
        var newWithoutDates = Export("Paid").Replace("Income,Cost", "Income,Cost,Last Paid,Next Charge");
        foreach (var raw in new[] { legacyWithDates, newWithoutDates })
        {
            var error = await Assert.ThrowsAsync<DomainRuleException>(() => service.SyncAsync(new(raw), "owner-1", default));
            Assert.Equal("RENT_SYNC_ROW_INVALID", error.ErrorCode);
        }
    }

    private static string DatedExport(string status, string lastPaid = "Unknown", string nextCharge = "10/22/2026") =>
        Export(status).Replace("Income,Cost", "Income,Cost,Last Paid,Next Charge") + $",{lastPaid},{nextCharge}";

    [Fact]
    public async Task Eviction_queue_batches_property_reads_for_large_queues_and_preserves_holds()
    {
        var values = Enumerable.Range(1, 100).Select(index =>
        {
            var property = new Property { Id = $"property-{index}", PropertyId = index, PropertyName = $"Home {index}" };
            property.SetTenantForPersistence($"tenant-{index}");
            property.SetStatusForPersistence(PropertyStatus.Evictable);
            return property;
        }).ToArray();
        var propertyRepository = new PropertyRepository(values);
        var snapshots = new SnapshotRepository();
        for (var index = 0; index < 200; index++)
            await snapshots.SaveCurrentAsync(new RentSyncSnapshot { Records = [] }, default);
        await snapshots.SaveCurrentAsync(new RentSyncSnapshot
        {
            Records = values.Select((property, index) => new RentSyncRecord
            {
                PropertyId = property.Id, Address = property.PropertyName, RowNumber = index + 1,
                Status = "evictable", NoticeGenerated = true, IsResolved = true,
                ResolvedAt = DateTime.UtcNow.AddHours(-26), IsEvictionOnHold = index % 10 == 0,
            }).ToList(),
        }, default);
        var service = new RentSyncService(snapshots, new TenantRepository(), propertyRepository,
            new LifecycleStore(), new StatusHistoryRepository(), new UserRepository(),
            new GoogleSheetsSyncService(), new AuditRepository());

        var queue = await service.GetEvictionQueueAsync(default);

        Assert.Equal(100, queue.Count);
        Assert.Equal(90, queue.Count(value => value.IsReady));
        Assert.Equal(10, queue.Count(value => value.IsOnHold));
        Assert.Equal(1, propertyRepository.BatchReads);
        Assert.Equal(0, propertyRepository.SingleReads);
        Assert.Equal(1, snapshots.StreamedSnapshots);
        Assert.Equal(0, snapshots.FullHistoryReads);
    }

    [Fact]
    public async Task Notice_feed_pages_only_generated_notices_but_keeps_whole_history_totals()
    {
        var snapshots = new SnapshotRepository();
        var dayStart = DateTime.UtcNow.Date;
        await snapshots.SaveCurrentAsync(new RentSyncSnapshot
        {
            Records = Enumerable.Range(1, 120).Select(index => new RentSyncRecord
            {
                RowNumber = index, Address = $"Home {index}", Status = "overdue", NoticeGenerated = true,
                IsResolved = index <= 20, ResolvedAt = index <= 20 ? dayStart.AddHours(1) : null,
            }).Concat([
                new RentSyncRecord { RowNumber = 121, Status = "paid" },
                new RentSyncRecord { RowNumber = 122, Status = "overdue", NoticeGenerated = false },
            ]).ToList(),
        }, default);
        var service = CreateService(snapshots);
        var first = await service.GetNoticeFeedAsync("overdue", 0, 50, null, default);
        var totals = await service.GetNoticeSummaryAsync("overdue", dayStart, dayStart.AddDays(1), default);
        Assert.Equal(50, first.Items.Count);
        Assert.Equal(121, totals.CurrentTotal);
        Assert.Equal(120, totals.TotalNotices);
        Assert.Equal(100, totals.Pending);
        Assert.Equal(20, totals.SentToday);
        Assert.True(first.HasMore);
        Assert.Equal(50, first.NextSkip);
        // A newer import must not shift page offsets for an already opened history.
        await snapshots.SaveCurrentAsync(new RentSyncSnapshot { Records = [new RentSyncRecord
            { RowNumber = 1, Status = "overdue", NoticeGenerated = true }] }, default);
        var last = await service.GetNoticeFeedAsync("overdue", 100, 50, first.AsOf, default);
        Assert.Equal(20, last.Items.Count);
        Assert.False(last.HasMore);
        Assert.Equal(120, last.NextSkip);
        Assert.All(last.Items, item => Assert.NotNull(item.Record.OverdueNotice));

        await snapshots.DeleteAsync("snapshot-2", default);
        var page = await service.GetCurrentRecordsAsync("overdue", 100, 50, default);
        Assert.Equal(122, page.Snapshot.Total);
        Assert.Equal(121, page.Snapshot.Overdue);
        Assert.Equal(21, page.Snapshot.Records.Count);
        Assert.False(page.HasMore);
    }

    [Theory]
    [InlineData("paid")]
    [InlineData("overdue")]
    [InlineData("empty")]
    public async Task Streaming_queue_stops_at_a_legacy_cycle_boundary(string status)
    {
        var snapshots = new SnapshotRepository();
        await snapshots.SaveCurrentAsync(new RentSyncSnapshot { Records = [new RentSyncRecord
        {
            Address = "Marina Drive 8", Status = "evictable", NoticeGenerated = null,
            IsResolved = true, ResolvedAt = DateTime.UtcNow.AddDays(-8), IsEvictionOnHold = true,
        }] }, default);
        await snapshots.SaveCurrentAsync(new RentSyncSnapshot { Records = [new RentSyncRecord
        { Address = " marina  drive 8 ", Status = status }] }, default);
        await snapshots.SaveCurrentAsync(new RentSyncSnapshot { Records = [new RentSyncRecord
        { PropertyId = "property-1", Address = "Marina Drive 8", Status = "evictable", NoticeGenerated = false }] }, default);
        var property = new Property { Id = "property-1", PropertyName = "Marina Drive 8" };
        property.SetStatusForPersistence(PropertyStatus.Evictable);
        property.SetTenantForPersistence("tenant-1");
        var service = new RentSyncService(snapshots, new TenantRepository(), new PropertyRepository(property),
            new LifecycleStore(), new StatusHistoryRepository(), new UserRepository(),
            new GoogleSheetsSyncService(), new AuditRepository());
        Assert.Empty(await service.GetEvictionQueueAsync(default));
        Assert.Equal(2, snapshots.StreamedSnapshots);
        Assert.Equal(0, snapshots.FullHistoryReads);
    }

    [Fact]
    public async Task Sync_parses_unquoted_currency_and_maps_discord_id_by_cid()
    {
        var snapshots = new SnapshotRepository();
        var tenant = new Tenant { Id = "tenant-1", PropertyId = "property-1", Cid = 1258, DiscordId = "727075012489510944" };
        var property = new Property { Id = "property-1", PropertyName = "Forum Drive 10 / Apt2" };
        property.SetTenantForPersistence(tenant.Id);
        property.SetStatusForPersistence(PropertyStatus.Paid);
        var googleSheets = new GoogleSheetsSyncService();
        var service = new RentSyncService(
            snapshots,
            new TenantRepository(tenant),
            new PropertyRepository(property),
            new LifecycleStore(),
            new StatusHistoryRepository(),
            new UserRepository(new User { Id = "owner-1", DisplayName = "Divine", Role = UserRole.Owner }),
            googleSheets,
            new AuditRepository());
        const string export = """
            Status,Address,Interior,Renter CID,Renter Name,Phone,Income,Cost
            Paid 4/26/2026,Marina Drive 1,Trevor's Trailer,21045,Nikhil Rajput,930-1260,$2,000,$1,000
            Overdue,Marina Drive 8,Trevor's Trailer,99,Ashwin Patil,333-4444,$2,000,$1,000
            Evictable,Forum Drive 10 / Apt2,Low-End Apartment,1258,Pintu Singh,322-4317,$3,000,$2,000
            Empty,Marina Drive 5,Trevor's Trailer,N/A,N/A,N/A,$0,$1,000
            """;

        var result = await service.SyncAsync(new RentSyncRequest(export), "owner-1", CancellationToken.None);

        Assert.Equal(4, result.Total);
        Assert.Equal(1, result.Active);
        Assert.Equal(1, result.Overdue);
        Assert.Equal(1, result.Evictable);
        Assert.Equal(1, result.Empty);
        var eviction = Assert.Single(result.Records.Where(x => x.Status == "evictable"));
        Assert.Equal("727075012489510944", eviction.DiscordId);
        Assert.Contains("Notify : <@727075012489510944>", eviction.EvictionNotice);
        Assert.Contains("Rent : $3,000", eviction.EvictionNotice);
        Assert.Equal(PropertyStatus.Evictable, property.Status);
        Assert.Equal("synced", result.GoogleSheetSyncStatus);
        Assert.Equal(4, googleSheets.PublishedRecords.Count);

        var resolved = await service.SetResolutionAsync(result.Id, eviction.RowNumber, true, "owner-1", CancellationToken.None);
        var resolvedEviction = Assert.Single(resolved.Records.Where(x => x.Status == "evictable"));
        Assert.True(resolvedEviction.IsResolved);
        Assert.Equal("Divine", resolvedEviction.ResolvedByDisplayName);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Sync_preserves_a_property_manual_on_hold_status(bool dated)
    {
        var property = new Property { Id = "property-1", PropertyName = "Marina Drive 8" };
        property.PlaceOnHold("Reserved for maintenance");
        var service = new RentSyncService(
            new SnapshotRepository(),
            new TenantRepository(),
            new PropertyRepository(property),
            new LifecycleStore(),
            new StatusHistoryRepository(),
            new UserRepository(new User { Id = "owner-1", DisplayName = "Divine", Role = UserRole.Owner }),
            new GoogleSheetsSyncService(),
            new AuditRepository());

        await service.SyncAsync(
            new RentSyncRequest(dated ? DatedExport("Empty", "N/A", "N/A") : Export("Empty")),
            "owner-1",
            default);

        Assert.Equal(PropertyStatus.OnHold, property.Status);
        Assert.Equal("Reserved for maintenance", property.UnavailableReason);
        Assert.Null(property.CurrentTenantId);
    }

    [Fact]
    public async Task Sync_generates_notices_only_when_a_property_enters_a_notice_status()
    {
        var snapshots = new SnapshotRepository();
        var service = CreateService(snapshots);

        var firstOverdue = await service.SyncAsync(
            new RentSyncRequest(Export("Overdue")), "owner-1", default);
        var repeatedOverdue = await service.SyncAsync(
            new RentSyncRequest(Export("Overdue")), "owner-1", default);
        var becameEvictable = await service.SyncAsync(
            new RentSyncRequest(Export("Evictable")), "owner-1", default);
        var repeatedEvictable = await service.SyncAsync(
            new RentSyncRequest(Export("Evictable")), "owner-1", default);
        await service.SyncAsync(
            new RentSyncRequest(Export("Paid 8/30/2026")), "owner-1", default);
        var overdueAfterPayment = await service.SyncAsync(
            new RentSyncRequest(Export("Overdue")), "owner-1", default);
        var previouslyAbsent = await service.SyncAsync(
            new RentSyncRequest(Export("Evictable", "New Property 2")), "owner-1", default);

        Assert.NotNull(Assert.Single(firstOverdue.Records).OverdueNotice);
        Assert.Null(Assert.Single(repeatedOverdue.Records).OverdueNotice);
        Assert.NotNull(Assert.Single(becameEvictable.Records).EvictionNotice);
        Assert.Null(Assert.Single(repeatedEvictable.Records).EvictionNotice);
        Assert.NotNull(Assert.Single(overdueAfterPayment.Records).OverdueNotice);
        Assert.NotNull(Assert.Single(previouslyAbsent.Records).EvictionNotice);
    }

    [Fact]
    public async Task Historical_unresolved_notice_uses_discord_id_added_after_it_was_generated()
    {
        var snapshots = new SnapshotRepository();
        var tenant = new Tenant
        {
            Id = "tenant-1",
            PropertyId = "property-1",
            Cid = 99,
            DiscordId = string.Empty,
            Status = TenantStatus.Active,
        };
        var property = new Property { Id = "property-1", PropertyName = "Marina Drive 8" };
        property.SetTenantForPersistence(tenant.Id);
        property.SetStatusForPersistence(PropertyStatus.Paid);
        var service = new RentSyncService(
            snapshots,
            new TenantRepository(tenant),
            new PropertyRepository(property),
            new LifecycleStore(),
            new StatusHistoryRepository(),
            new UserRepository(new User { Id = "owner-1", DisplayName = "Divine", Role = UserRole.Owner }),
            new GoogleSheetsSyncService(),
            new AuditRepository());

        var first = await service.SyncAsync(
            new RentSyncRequest(Export("Evictable")), "owner-1", default);
        Assert.Contains("Discord ID unavailable", Assert.Single(first.Records).EvictionNotice);

        tenant.DiscordId = "727075012489510944";
        await service.SyncAsync(new RentSyncRequest(Export("Evictable")), "owner-1", default);

        var history = await service.GetAllAsync(default);
        var originalNotice = Assert.Single(history
            .SelectMany(snapshot => snapshot.Records)
            .Where(record => record.EvictionNotice is not null));
        Assert.Equal("727075012489510944", originalNotice.DiscordId);
        Assert.Contains("Notify : <@727075012489510944>", originalNotice.EvictionNotice);
    }

    [Fact]
    public async Task Eviction_queue_starts_twenty_four_hours_after_notice_is_sent()
    {
        var snapshots = new SnapshotRepository();
        var tenant = new Tenant
        {
            Id = "tenant-1",
            PropertyId = "property-1",
            Cid = 99,
            DiscordId = "727075012489510944"
        };
        var property = new Property { Id = "property-1", PropertyId = 8, PropertyName = "Marina Drive 8" };
        property.SetTenantForPersistence(tenant.Id);
        property.SetStatusForPersistence(PropertyStatus.Paid);
        var service = new RentSyncService(
            snapshots,
            new TenantRepository(tenant),
            new PropertyRepository(property),
            new LifecycleStore(),
            new StatusHistoryRepository(),
            new UserRepository(new User { Id = "owner-1", DisplayName = "Divine", Role = UserRole.Owner }),
            new GoogleSheetsSyncService(),
            new AuditRepository());

        var synced = await service.SyncAsync(
            new RentSyncRequest(Export("Evictable")), "owner-1", default);
        Assert.Empty(await service.GetEvictionQueueAsync(default));

        await service.SetResolutionAsync(synced.Id, 1, true, "owner-1", default);
        var waiting = Assert.Single(await service.GetEvictionQueueAsync(default));
        Assert.False(waiting.IsReady);

        var stored = await snapshots.GetByIdAsync(synced.Id, default);
        Assert.NotNull(stored);
        Assert.Single(stored.Records).ResolvedAt = DateTime.UtcNow.AddHours(-25);
        await snapshots.UpdateAsync(stored, default);

        var ready = Assert.Single(await service.GetEvictionQueueAsync(default));
        Assert.True(ready.IsReady);
        Assert.Equal("property-1", ready.PropertyId);
    }

    [Fact]
    public async Task Eviction_queue_hold_blocks_eviction_until_released()
    {
        var snapshots = new SnapshotRepository();
        var tenant = new Tenant
        {
            Id = "tenant-1",
            PropertyId = "property-1",
            Cid = 99,
            DiscordId = "727075012489510944"
        };
        var property = new Property { Id = "property-1", PropertyId = 8, PropertyName = "Marina Drive 8" };
        property.SetTenantForPersistence(tenant.Id);
        property.SetStatusForPersistence(PropertyStatus.Paid);
        var service = new RentSyncService(
            snapshots,
            new TenantRepository(tenant),
            new PropertyRepository(property),
            new LifecycleStore(),
            new StatusHistoryRepository(),
            new UserRepository(new User { Id = "owner-1", DisplayName = "Divine", Role = UserRole.Owner }),
            new GoogleSheetsSyncService(),
            new AuditRepository());

        var synced = await service.SyncAsync(new RentSyncRequest(Export("Evictable")), "owner-1", default);
        await service.SetResolutionAsync(synced.Id, 1, true, "owner-1", default);
        var stored = await snapshots.GetByIdAsync(synced.Id, default);
        Assert.NotNull(stored);
        Assert.Single(stored.Records).ResolvedAt = DateTime.UtcNow.AddHours(-25);
        await snapshots.UpdateAsync(stored, default);

        await service.SetEvictionHoldAsync(synced.Id, 1, true, "owner-1", default);
        var held = Assert.Single(await service.GetEvictionQueueAsync(default));
        Assert.True(held.IsOnHold);
        Assert.False(held.IsReady);
        Assert.Equal("Divine", held.HeldByDisplayName);

        await service.SetEvictionHoldAsync(synced.Id, 1, false, "owner-1", default);
        var released = Assert.Single(await service.GetEvictionQueueAsync(default));
        Assert.False(released.IsOnHold);
        Assert.True(released.IsReady);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Paid_sync_closes_the_eviction_cycle_and_a_later_cycle_gets_a_new_notice(bool dated)
    {
        var snapshots = new SnapshotRepository();
        var tenant = new Tenant
        {
            Id = "tenant-1",
            PropertyId = "property-1",
            Cid = 99,
            DiscordId = "727075012489510944",
            Status = TenantStatus.Active,
        };
        var property = new Property { Id = "property-1", PropertyId = 8, PropertyName = "Marina Drive 8" };
        property.SetTenantForPersistence(tenant.Id);
        property.SetStatusForPersistence(PropertyStatus.Paid);
        var service = new RentSyncService(
            snapshots,
            new TenantRepository(tenant),
            new PropertyRepository(property),
            new LifecycleStore(),
            new StatusHistoryRepository(),
            new UserRepository(new User { Id = "owner-1", DisplayName = "Divine", Role = UserRole.Owner }),
            new GoogleSheetsSyncService(),
            new AuditRepository());

        var firstCycle = await service.SyncAsync(
            new RentSyncRequest(Export("Evictable")), "owner-1", default);
        await service.SetResolutionAsync(firstCycle.Id, 1, true, "owner-1", default);
        await service.SetEvictionHoldAsync(firstCycle.Id, 1, true, "owner-1", default);
        Assert.True(Assert.Single(await service.GetEvictionQueueAsync(default)).IsOnHold);

        var paid = await service.SyncAsync(
            new RentSyncRequest(dated ? DatedExport("Paid") : Export("Paid")), "owner-1", default);

        Assert.Equal(PropertyStatus.Paid, property.Status);
        Assert.Equal("paid", Assert.Single(paid.Records).Status);
        Assert.Empty(await service.GetEvictionQueueAsync(default));
        var historicalCycle = await snapshots.GetByIdAsync(firstCycle.Id, default);
        Assert.True(Assert.Single(historicalCycle!.Records).IsEvictionOnHold);

        var secondCycle = await service.SyncAsync(
            new RentSyncRequest(Export("Evictable")), "owner-1", default);
        var secondNotice = Assert.Single(secondCycle.Records);

        Assert.NotNull(secondNotice.EvictionNotice);
        Assert.False(secondNotice.IsResolved);
        Assert.Empty(await service.GetEvictionQueueAsync(default));

        await service.SetResolutionAsync(secondCycle.Id, 1, true, "owner-1", default);
        var waiting = Assert.Single(await service.GetEvictionQueueAsync(default));
        Assert.False(waiting.IsOnHold);
        Assert.False(waiting.IsReady);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Overdue_sync_closes_the_eviction_cycle_and_a_later_cycle_gets_a_new_notice(bool dated)
    {
        var snapshots = new SnapshotRepository();
        var tenant = new Tenant
        {
            Id = "tenant-1",
            PropertyId = "property-1",
            Cid = 99,
            DiscordId = "727075012489510944",
            Status = TenantStatus.Active,
        };
        var property = new Property { Id = "property-1", PropertyId = 8, PropertyName = "Marina Drive 8" };
        property.SetTenantForPersistence(tenant.Id);
        property.SetStatusForPersistence(PropertyStatus.Paid);
        var service = new RentSyncService(
            snapshots,
            new TenantRepository(tenant),
            new PropertyRepository(property),
            new LifecycleStore(),
            new StatusHistoryRepository(),
            new UserRepository(new User { Id = "owner-1", DisplayName = "Divine", Role = UserRole.Owner }),
            new GoogleSheetsSyncService(),
            new AuditRepository());

        var firstCycle = await service.SyncAsync(
            new RentSyncRequest(Export("Evictable")), "owner-1", default);
        await service.SetResolutionAsync(firstCycle.Id, 1, true, "owner-1", default);
        await service.SetEvictionHoldAsync(firstCycle.Id, 1, true, "owner-1", default);

        var overdue = await service.SyncAsync(
            new RentSyncRequest(dated ? DatedExport("Overdue") : Export("Overdue")), "owner-1", default);

        Assert.Equal(PropertyStatus.Overdue, property.Status);
        Assert.NotNull(Assert.Single(overdue.Records).OverdueNotice);
        Assert.Empty(await service.GetEvictionQueueAsync(default));

        var secondCycle = await service.SyncAsync(
            new RentSyncRequest(Export("Evictable")), "owner-1", default);
        var secondNotice = Assert.Single(secondCycle.Records);

        Assert.NotNull(secondNotice.EvictionNotice);
        Assert.False(secondNotice.IsResolved);
        Assert.Empty(await service.GetEvictionQueueAsync(default));

        await service.SetResolutionAsync(secondCycle.Id, 1, true, "owner-1", default);
        var waiting = Assert.Single(await service.GetEvictionQueueAsync(default));
        Assert.False(waiting.IsOnHold);
        Assert.False(waiting.IsReady);
    }

    private static RentSyncService CreateService(SnapshotRepository snapshots) => new(
        snapshots,
        new TenantRepository(),
        new PropertyRepository(),
        new LifecycleStore(),
        new StatusHistoryRepository(),
        new UserRepository(new User { Id = "owner-1", DisplayName = "Divine", Role = UserRole.Owner }),
        new GoogleSheetsSyncService(),
        new AuditRepository());

    private static string Export(string status, string address = "Marina Drive 8") => $"""
        Status,Address,Interior,Renter CID,Renter Name,Phone,Income,Cost
        {status},{address},Trevor's Trailer,99,Ashwin Patil,333-4444,$2,000,$1,000
        """;

    [Fact]
    public async Task Sync_imports_missing_tenants_by_address_and_supports_multiple_properties_per_cid()
    {
        var snapshots = new SnapshotRepository();
        var knownTenant = new Tenant
        {
            Id = "old-tenant",
            PropertyId = "old-property",
            Cid = 1002,
            DiscordId = "727075012489510944",
            Status = TenantStatus.Evicted,
        };
        var firstProperty = new Property
        {
            Id = "property-22",
            PropertyId = 22,
            PropertyName = "South Mo Milton Drive 22",
        };
        var secondProperty = new Property
        {
            Id = "property-22b",
            PropertyId = 23,
            PropertyName = "South Mo Milton Drive 22b",
        };
        var lifecycle = new LifecycleStore();
        var service = new RentSyncService(
            snapshots,
            new TenantRepository(knownTenant),
            new PropertyRepository(firstProperty, secondProperty),
            lifecycle,
            new StatusHistoryRepository(),
            new UserRepository(new User { Id = "owner-1", DisplayName = "Divine", Role = UserRole.Owner }),
            new GoogleSheetsSyncService(),
            new AuditRepository());
        const string export = """
            Status,Address,Interior,Renter CID,Renter Name,Phone,Income,Cost
            Paid 9/2/2026,South Mo Milton Drive 22,Mid-End Apartment (House),1002,Baali Singh,151-6364,$7,000,$4,500
            Overdue,South Mo Milton Drive 22b,Low-End Apartment,1002,Baali Singh,151-6364,$3,000,$2,000
            """;

        var result = await service.SyncAsync(new RentSyncRequest(export), "owner-1", default);

        Assert.Equal(2, lifecycle.ImportedTenants.Count);
        Assert.Equal(2, lifecycle.ImportedTenants.Select(x => x.PropertyId).Distinct().Count());
        Assert.All(lifecycle.ImportedTenants, tenant =>
        {
            Assert.Equal(1002, tenant.Cid);
            Assert.Equal("727075012489510944", tenant.DiscordId);
        });
        Assert.Equal(PropertyStatus.Paid, firstProperty.Status);
        Assert.Equal(PropertyStatus.Overdue, secondProperty.Status);
        Assert.Equal(7_000m, firstProperty.Rent);
        Assert.Equal(3_000m, secondProperty.Rent);
        Assert.All(result.Records, record =>
        {
            Assert.NotNull(record.TenantId);
        });
    }

    private sealed class SnapshotRepository : IRentSyncRepository
    {
        private readonly List<RentSyncSnapshot> _snapshots = [];
        public int StreamedSnapshots { get; private set; }
        public int FullHistoryReads { get; private set; }
        public async IAsyncEnumerable<RentSyncSnapshot> ReadEvictionHistoryAsync(DateTime asOf,
            IReadOnlyCollection<string> propertyIds, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            foreach (var snapshot in _snapshots.AsEnumerable().Reverse().Where(value => value.UpdatedAt <= asOf))
            {
                ct.ThrowIfCancellationRequested();
                StreamedSnapshots++;
                yield return snapshot;
            }
        }
        public Task<RentSyncSnapshot?> GetCurrentAsync(CancellationToken cancellationToken) => Task.FromResult(_snapshots.LastOrDefault());
        public Task<RentSyncSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_snapshots.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<RentSyncSnapshot>> GetAllAsync(CancellationToken cancellationToken)
        {
            FullHistoryReads++;
            return Task.FromResult<IReadOnlyList<RentSyncSnapshot>>(_snapshots.AsEnumerable().Reverse().ToList());
        }
        public Task SaveCurrentAsync(RentSyncSnapshot snapshot, CancellationToken cancellationToken)
        {
            snapshot.Id = $"snapshot-{_snapshots.Count + 1}";
            snapshot.UpdatedAt = DateTime.UtcNow.AddTicks(_snapshots.Count);
            _snapshots.Add(snapshot);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(RentSyncSnapshot snapshot, CancellationToken cancellationToken)
        {
            var index = _snapshots.FindIndex(x => x.Id == snapshot.Id);
            if (index >= 0) _snapshots[index] = snapshot;
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string id, CancellationToken cancellationToken)
        {
            _snapshots.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class UserRepository(params User[] values) : IUserRepository
    {
        public Task<User?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult(values.FirstOrDefault(x => x.Id == id));
        public Task<PagedResult<User>> QueryAsync(int page, int pageSize, ApprovalStatus? approval, AccessStatus? access, UserRole? role, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByDiscordIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<User?> GetByCidAsync(int cid, CancellationToken ct) => Task.FromResult(values.FirstOrDefault(x => x.Cid == cid));
        public Task<long> CountActiveManagersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountPendingAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(User user, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(User user, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class TenantRepository(params Tenant[] values) : ITenantRepository
    {
        public Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Tenant>>(values);
        public Task<IReadOnlyList<Tenant>> GetByCidsAsync(IReadOnlyCollection<int> cids, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Tenant>>(values.Where(x => x.Cid.HasValue && cids.Contains(x.Cid.Value)).ToList());
        public Task<IReadOnlyList<Tenant>> GetEvictedAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Tenant>>([]);
        public Task<PagedResult<Tenant>> QueryAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(values.FirstOrDefault(x => x.Id == id));
        public Task CreateAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class PropertyRepository(params Property[] values) : IPropertyRepository
    {
        public int BatchReads { get; private set; }
        public int SingleReads { get; private set; }
        public Task<Property?> GetByIdAsync(string id, CancellationToken ct)
        {
            SingleReads++;
            return Task.FromResult(values.FirstOrDefault(x => x.Id == id));
        }
        public Task<Property?> GetByNameAsync(string name, CancellationToken ct) => Task.FromResult(values.FirstOrDefault(x => x.PropertyName.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task UpdateAsync(Property property, CancellationToken ct) => Task.CompletedTask;
        public Task<PagedResult<Property>> QueryAsync(PropertyQuery query, bool publicOnly, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Property>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
        {
            BatchReads++;
            return Task.FromResult<IReadOnlyList<Property>>(values.Where(value => ids.Contains(value.Id)).ToList());
        }
        public Task<Property?> GetByBusinessIdAsync(int propertyId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Property>> GetAllAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Property>>(values);
        public Task<IReadOnlyList<Property>> GetFeaturedAsync(int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountByBlockAsync(string blockId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountByStatusAsync(PropertyStatus? status, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(Property property, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class StatusHistoryRepository : IStatusHistoryRepository
    {
        public Task CreateAsync(PropertyStatusHistory history, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<PropertyStatusHistory>> GetByPropertyAsync(string propertyId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PropertyStatusHistory>> GetRecentAsync(int limit, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class LifecycleStore : IPropertyLifecycleStore
    {
        public List<Tenant> ImportedTenants { get; } = [];

        public Task AssignTenantAsync(
            Property property,
            Tenant tenant,
            PropertyStatusHistory history,
            AuditLog audit,
            CommissionRecord? commission,
            CancellationToken cancellationToken)
        {
            ImportedTenants.Add(tenant);
            return Task.CompletedTask;
        }

        public Task UpdateTenantAsync(
            Property property, Tenant tenant, AuditLog audit, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task EvictAsync(
            Property property,
            Tenant tenant,
            PropertyStatusHistory history,
            AuditLog audit,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AuditRepository : IAuditRepository
    {
        public Task CreateAsync(AuditLog auditLog, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<PagedResult<AuditLog>> QueryAsync(int page, int pageSize, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class GoogleSheetsSyncService : IGoogleSheetsSyncService
    {
        public bool IsConfigured => true;
        public string? SpreadsheetUrl => "https://docs.google.com/spreadsheets/d/test/edit?gid=0";
        public IReadOnlyList<RentSyncRecord> PublishedRecords { get; private set; } = [];
        public Task PublishAsync(IReadOnlyList<RentSyncRecord> records, CancellationToken cancellationToken)
        {
            PublishedRecords = records;
            return Task.CompletedTask;
        }
    }
}
