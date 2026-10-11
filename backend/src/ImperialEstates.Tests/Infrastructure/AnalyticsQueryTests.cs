using ImperialEstates.Infrastructure.Persistence;
using ImperialEstates.Infrastructure.Repositories;
using Microsoft.Extensions.Options;

namespace ImperialEstates.Tests.Infrastructure;

public sealed class AnalyticsQueryTests
{
    private static MongoContext Context() => new(Options.Create(new MongoOptions
    {
        ConnectionString = "mongodb://localhost:27017",
        DatabaseName = "query_translation_only",
    }));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Analytics_queries_translate_dates_and_projections_without_database_access(bool hasFrom, bool hasTo)
    {
        var db = Context();
        DateTime? from = hasFrom ? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null;
        DateTime? to = hasTo ? new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc) : null;
        var bookings = new PropertyBookingRepository(db).AnalyticsQuery(from, to).ToString()!;
        var tenants = new TenantRepository(db).AnalyticsQuery(from, to).ToString()!;
        var commissions = new CommissionRepository(db).AnalyticsQuery(from, to).ToString()!;
        var notices = new RentSyncRepository(db).AnalyticsQuery(from, to).ToString()!;

        Assert.Contains("BookingAmount", bookings);
        Assert.DoesNotContain("PhoneNumber", bookings);
        Assert.Contains("EndDate", tenants);
        Assert.DoesNotContain("EvictionStorageImages", tenants);
        Assert.Contains("SchemeVersion", commissions);
        Assert.Contains("PaidAt", commissions);
        Assert.Contains("$filter", notices);
        Assert.Contains("ResolvedAt", notices);
        Assert.DoesNotContain("GoogleSheetSyncError", notices);
    }

    [Fact]
    public void Dashboard_income_is_summed_by_mongodb_instead_of_returning_snapshot_records()
    {
        var query = new RentSyncRepository(Context()).IncomeQuery().ToString()!;
        Assert.Contains("$sum", query);
        Assert.Contains("UpdatedAt", query);
        Assert.DoesNotContain("Phone", query);
    }

    [Fact]
    public void Portfolio_totals_do_not_download_images_descriptions_or_booking_details()
    {
        var query = new PropertyRepository(Context()).PortfolioQuery().ToString()!;
        Assert.Contains("Rent", query);
        Assert.Contains("Status", query);
        Assert.DoesNotContain("Images", query);
        Assert.DoesNotContain("Description", query);
        Assert.DoesNotContain("BookingDetails", query);
    }

    [Fact]
    public void Sync_history_returns_counts_instead_of_every_export_row()
    {
        var query = new RentSyncRepository(Context()).SummaryQuery().ToString()!;
        Assert.Contains("$size", query);
        Assert.Contains("Status", query);
        Assert.DoesNotContain("\"Records\" : 1", query);
        Assert.DoesNotContain("Phone", query);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Eviction_history_projection_keeps_cycle_boundaries_and_hold_state(bool legacy)
    {
        var query = new RentSyncRepository(Context()).EvictionHistoryQuery(DateTime.UtcNow,
            legacy ? [] : ["property-1", "property-2"]).ToString()!;
        Assert.Contains("Status", query);
        Assert.Contains("IsEvictionOnHold", query);
        Assert.DoesNotContain("Phone", query);
        Assert.DoesNotContain("Income", query);
        Assert.DoesNotContain("\"Status\" : \"evictable\"", query);
    }

    [Fact]
    public void Notice_feed_filters_before_paging_and_does_not_return_whole_exports()
    {
        var repository = new RentSyncRepository(Context());
        var query = repository.NoticePageQuery("overdue", DateTime.UtcNow, 50, 51).ToString()!;
        Assert.Contains("$unwind", query);
        Assert.Contains("$filter", query);
        Assert.Contains("NoticeGenerated", query);
        Assert.Contains("$skip", query);
        Assert.Contains("$limit", query);
        Assert.DoesNotContain("Expression not supported", query);
        var counts = repository.NoticeCountsQuery("overdue", DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(1)).ToString()!;
        Assert.Contains("$sum", counts);
        Assert.DoesNotContain("Phone", counts);
        Assert.DoesNotContain("Interior", counts);
    }

    [Fact]
    public void Overdue_records_are_filtered_and_sliced_in_the_database()
    {
        var query = new RentSyncRepository(Context()).CurrentRecordsQuery("overdue", 50, 50).ToString()!;
        Assert.Contains("$filter", query);
        Assert.Contains("$slice", query);
        Assert.Contains("overdue", query);
        Assert.DoesNotContain("Expression not supported", query);
    }
}
