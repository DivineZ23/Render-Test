using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Infrastructure.Persistence;
using MongoDB.Bson;
using MongoDB.Driver;

namespace ImperialEstates.Infrastructure.Repositories;

public sealed class PropertyRepository(MongoContext db) : IPropertyRepository
{
    public async Task<IReadOnlyList<EvictionPropertyLookup>> GetEvictionCandidatesAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        await db.Properties.Find(value => !value.IsDeleted && (ids.Count == 0 || ids.Contains(value.Id)))
            .Project(value => new EvictionPropertyLookup(value.Id, value.PropertyId, value.PropertyName,
                value.Status, value.CurrentTenantId)).ToListAsync(ct);
    public async Task<PagedResult<Property>> QueryAsync(PropertyQuery query, bool publicOnly, CancellationToken ct)
    {
        var f = Builders<Property>.Filter;
        var filter = f.Eq(x => x.IsDeleted, false);
        if (publicOnly) filter &= f.Eq(x => x.IsActive, true) & f.Eq(x => x.Status, PropertyStatus.Available);
        else if (query.Status.HasValue) filter &= f.Eq(x => x.Status, query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.BlockId)) filter &= f.Eq(x => x.BlockId, query.BlockId);
        if (query.Type.HasValue) filter &= f.Eq(x => x.Type, query.Type.Value);
        if (query.MinRent.HasValue) filter &= f.Gte(x => x.Rent, query.MinRent.Value);
        if (query.MaxRent.HasValue) filter &= f.Lte(x => x.Rent, query.MaxRent.Value);
        if (query.Bedrooms.HasValue) filter &= f.Eq(x => x.Bedrooms, query.Bedrooms.Value);
        if (query.PersonCapacity.HasValue)
        {
            var types = Enum.GetValues<PropertyType>()
                .Where(type => type.PersonCapacity() == query.PersonCapacity.Value);
            filter &= f.In(x => x.Type, types);
        }
        if (query.StorageCapacity.HasValue)
        {
            var types = Enum.GetValues<PropertyType>()
                .Where(type => type.StorageCapacity() == query.StorageCapacity.Value);
            filter &= f.In(x => x.Type, types);
        }
        if (!string.IsNullOrWhiteSpace(query.Furnishing)) filter &= f.Eq(x => x.FurnishingStatus, query.Furnishing);
        if (query.Amenities.Length > 0) filter &= f.All(x => x.Amenities, query.Amenities);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(query.Search.Trim()), "i");
            var matchingBlocks = await db.Blocks.Find(Builders<Block>.Filter.Regex(x => x.BlockName, regex)).Project(x => x.Id).ToListAsync(ct);
            var numeric = int.TryParse(query.Search, out var propertyId);
            var searchFilters = new List<FilterDefinition<Property>>
            {
                f.Regex(x => x.PropertyName, regex),
                f.In(x => x.BlockId, matchingBlocks)
            };
            if (numeric) searchFilters.Add(f.Eq(x => x.PropertyId, propertyId));
            filter &= f.Or(searchFilters);
        }
        var page = Paging.NormalizePage(query.Page); var pageSize = Paging.NormalizePageSize(query.PageSize);
        var sort = BuildSort(query.SortBy, query.SortDirection);
        var totalTask = db.Properties.CountDocumentsAsync(filter, cancellationToken: ct);
        var itemsTask = db.Properties.Find(filter).Sort(sort).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        await Task.WhenAll(totalTask, itemsTask);
        return new(itemsTask.Result, page, pageSize, totalTask.Result);
    }

    public Task<Property?> GetByIdAsync(string id, CancellationToken ct) => db.Properties.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public async Task<IReadOnlyList<Property>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var filter = Builders<Property>.Filter.Eq(x => x.IsDeleted, false) & Builders<Property>.Filter.In(x => x.Id, ids);
        return await db.Properties.Find(filter).ToListAsync(ct);
    }
    public Task<Property?> GetByBusinessIdAsync(int id, CancellationToken ct) => db.Properties.Find(x => x.PropertyId == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public Task<Property?> GetByNameAsync(string name, CancellationToken ct)
    {
        var exactName = new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(name.Trim())}$", "i");
        var filter = Builders<Property>.Filter.Eq(x => x.IsDeleted, false) & Builders<Property>.Filter.Regex(x => x.PropertyName, exactName);
        return db.Properties.Find(filter).FirstOrDefaultAsync(ct)!;
    }
    public async Task<IReadOnlyList<Property>> GetAllAsync(CancellationToken ct) =>
        await db.Properties.Find(x => !x.IsDeleted).ToListAsync(ct);
    public async Task<IReadOnlyList<PropertyPortfolioItem>> GetPortfolioAsync(CancellationToken ct) =>
        await PortfolioQuery().ToListAsync(ct);
    internal IFindFluent<Property, PropertyPortfolioItem> PortfolioQuery() =>
        db.Properties.Find(x => !x.IsDeleted)
            .Project(x => new PropertyPortfolioItem(x.BlockId, x.Type, x.Rent, x.Status, x.CurrentTenantId));
    public async Task<IReadOnlyList<Property>> GetFeaturedAsync(int limit, CancellationToken ct) => await db.Properties.Find(x => !x.IsDeleted && x.IsActive && x.IsFeatured && x.Status == PropertyStatus.Available).SortByDescending(x => x.CreatedAt).Limit(limit).ToListAsync(ct);
    public Task<long> CountByBlockAsync(string blockId, CancellationToken ct) => db.Properties.CountDocumentsAsync(x => x.BlockId == blockId && !x.IsDeleted, cancellationToken: ct);
    public Task<long> CountByStatusAsync(PropertyStatus? status, CancellationToken ct)
    {
        var filter = Builders<Property>.Filter.Eq(x => x.IsDeleted, false);
        if (status.HasValue) filter &= Builders<Property>.Filter.Eq(x => x.Status, status.Value);
        return db.Properties.CountDocumentsAsync(filter, cancellationToken: ct);
    }

    public Task<long> CountPendingBookingAnnouncementsAsync(CancellationToken ct) =>
        db.Properties.CountDocumentsAsync(
            property => !property.IsDeleted && property.IsActive && property.BookingAnnouncementPending,
            cancellationToken: ct);
    public Task CreateAsync(Property value, CancellationToken ct) { RepositoryHelpers.PrepareForInsert(value); return db.Properties.InsertOneAsync(value, cancellationToken: ct); }
    public Task UpdateAsync(Property value, CancellationToken ct) => RepositoryHelpers.ReplaceAsync(db.Properties, value, ct);

    private static SortDefinition<Property> BuildSort(string sortBy, string direction)
    {
        var ascending = string.Equals(direction, "asc", StringComparison.OrdinalIgnoreCase);
        var s = Builders<Property>.Sort;
        return sortBy.ToLowerInvariant() switch
        {
            "id" or "propertyid" => ascending ? s.Ascending(x => x.PropertyId) : s.Descending(x => x.PropertyId),
            "rent" => ascending ? s.Ascending(x => x.Rent) : s.Descending(x => x.Rent),
            "name" => ascending ? s.Ascending(x => x.PropertyName) : s.Descending(x => x.PropertyName),
            "updated" => ascending ? s.Ascending(x => x.UpdatedAt) : s.Descending(x => x.UpdatedAt),
            _ => ascending ? s.Ascending(x => x.CreatedAt) : s.Descending(x => x.CreatedAt)
        };
    }
}
