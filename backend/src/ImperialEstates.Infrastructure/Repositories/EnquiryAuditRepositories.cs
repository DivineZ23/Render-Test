using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ImperialEstates.Infrastructure.Repositories;

public sealed class EnquiryRepository(MongoContext db) : IEnquiryRepository
{
    public async Task<PagedResult<Enquiry>> QueryAsync(int page, int pageSize, EnquiryStatus? status, CancellationToken ct)
    {
        page = Paging.NormalizePage(page); pageSize = Paging.NormalizePageSize(pageSize);
        var f = Builders<Enquiry>.Filter; var filter = f.Eq(x => x.IsDeleted, false);
        if (status.HasValue) filter &= f.Eq(x => x.Status, status.Value);
        var totalTask = db.Enquiries.CountDocumentsAsync(filter, cancellationToken: ct);
        var itemsTask = db.Enquiries.Find(filter).SortByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        await Task.WhenAll(totalTask, itemsTask);
        return new(itemsTask.Result, page, pageSize, totalTask.Result);
    }
    public Task<Enquiry?> GetByIdAsync(string id, CancellationToken ct) => db.Enquiries.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public Task<long> CountPendingAsync(CancellationToken ct) => db.Enquiries.CountDocumentsAsync(x => !x.IsDeleted && (x.Status == EnquiryStatus.New || x.Status == EnquiryStatus.Contacted), cancellationToken: ct);
    public Task CreateAsync(Enquiry value, CancellationToken ct) { RepositoryHelpers.PrepareForInsert(value); return db.Enquiries.InsertOneAsync(value, cancellationToken: ct); }
    public Task UpdateAsync(Enquiry value, CancellationToken ct) => RepositoryHelpers.ReplaceAsync(db.Enquiries, value, ct);
}

public sealed class AuditRepository(MongoContext db) : IAuditRepository
{
    public Task CreateAsync(AuditLog value, CancellationToken ct) { RepositoryHelpers.PrepareForInsert(value); return db.AuditLogs.InsertOneAsync(value, cancellationToken: ct); }
    public async Task<PagedResult<AuditLog>> QueryAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Paging.NormalizePage(page); pageSize = Paging.NormalizePageSize(pageSize);
        var filter = FilterDefinition<AuditLog>.Empty;
        var totalTask = db.AuditLogs.CountDocumentsAsync(filter, cancellationToken: ct);
        var itemsTask = db.AuditLogs.Find(filter).SortByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        await Task.WhenAll(totalTask, itemsTask);
        return new(itemsTask.Result, page, pageSize, totalTask.Result);
    }

    public async Task<PagedResult<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken ct)
    {
        var page = Paging.NormalizePage(query.Page);
        var pageSize = Paging.NormalizePageSize(query.PageSize);
        var f = Builders<AuditLog>.Filter;
        var filter = FilterDefinition<AuditLog>.Empty;

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var categoryFilter = query.Category.Trim().ToLowerInvariant() switch
            {
                "access" => PrefixFilter(f, "user.", "settings.", "recruitment."),
                "portfolio" => PrefixFilter(f, "block.", "property.", "tenant.", "enquiry.") &
                               f.Nin(x => x.Action, new[] { "property.status.synced", "tenant.imported-from-rent-sync" }),
                "finance" => PrefixFilter(f, "commission."),
                "operations" => PrefixFilter(f, "rent-data.", "google-sheet.", "notice.", "eviction.") |
                                f.In(x => x.Action, new[] { "property.status.synced", "tenant.imported-from-rent-sync" }),
                _ => FilterDefinition<AuditLog>.Empty
            };
            filter &= categoryFilter;
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
            filter &= f.Eq(x => x.EntityType, query.EntityType.Trim());
        if (!string.IsNullOrWhiteSpace(query.ActorId))
            filter &= f.Eq(x => x.PerformedByUserId, query.ActorId.Trim());
        if (query.From.HasValue) filter &= f.Gte(x => x.CreatedAt, query.From.Value);
        if (query.To.HasValue) filter &= f.Lte(x => x.CreatedAt, query.To.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var regex = new BsonRegularExpression(
                System.Text.RegularExpressions.Regex.Escape(query.Search.Trim()), "i");
            var searchFilters = new List<FilterDefinition<AuditLog>>
            {
                f.Regex(x => x.Action, regex),
                f.Regex(x => x.EntityType, regex),
                f.Regex(x => x.EntityId, regex),
                f.Regex(x => x.PerformedByUserId, regex)
            };
            if (query.MatchingActorIds.Count > 0)
                searchFilters.Add(f.In(x => x.PerformedByUserId, query.MatchingActorIds));
            if (query.MatchingEntityIds.Count > 0)
                searchFilters.Add(f.In(x => x.EntityId, query.MatchingEntityIds));
            filter &= f.Or(searchFilters);
        }

        var sort = query.SortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase)
            ? Builders<AuditLog>.Sort.Ascending(x => x.CreatedAt)
            : Builders<AuditLog>.Sort.Descending(x => x.CreatedAt);
        var totalTask = db.AuditLogs.CountDocumentsAsync(filter, cancellationToken: ct);
        var itemsTask = db.AuditLogs.Find(filter).Sort(sort).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        await Task.WhenAll(totalTask, itemsTask);
        return new(itemsTask.Result, page, pageSize, totalTask.Result);
    }

    private static FilterDefinition<AuditLog> PrefixFilter(
        FilterDefinitionBuilder<AuditLog> filters,
        params string[] prefixes) =>
        filters.Or(prefixes.Select(prefix => filters.Regex(x => x.Action,
            new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(prefix)}", "i"))));
}

public sealed class StatusHistoryRepository(MongoContext db) : IStatusHistoryRepository
{
    public Task CreateAsync(PropertyStatusHistory value, CancellationToken ct) { RepositoryHelpers.PrepareForInsert(value); return db.StatusHistory.InsertOneAsync(value, cancellationToken: ct); }
    public async Task<IReadOnlyList<PropertyStatusHistory>> GetByPropertyAsync(string id, CancellationToken ct) => await db.StatusHistory.Find(x => x.PropertyId == id).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    public async Task<IReadOnlyList<PropertyStatusHistory>> GetRecentAsync(int limit, CancellationToken ct) => await db.StatusHistory.Find(FilterDefinition<PropertyStatusHistory>.Empty).SortByDescending(x => x.CreatedAt).Limit(limit).ToListAsync(ct);
}
