using ImperialEstates.Application.Common;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Infrastructure.Persistence;
using MongoDB.Driver;

namespace ImperialEstates.Infrastructure.Repositories;

public sealed class TenantRepository(MongoContext db) : ITenantRepository
{
    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken ct) =>
        await db.Tenants.Find(x => !x.IsDeleted).SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<PagedResult<Tenant>> QueryAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Paging.NormalizePage(page); pageSize = Paging.NormalizePageSize(pageSize);
        var filter = Builders<Tenant>.Filter.Eq(x => x.IsDeleted, false);
        var totalTask = db.Tenants.CountDocumentsAsync(filter, cancellationToken: ct);
        var itemsTask = db.Tenants.Find(filter).SortByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        await Task.WhenAll(totalTask, itemsTask);
        return new(itemsTask.Result, page, pageSize, totalTask.Result);
    }
    public Task<Tenant?> GetByIdAsync(string id, CancellationToken ct) => db.Tenants.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public async Task<IReadOnlyList<Tenant>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var filter = Builders<Tenant>.Filter.Eq(x => x.IsDeleted, false) &
            Builders<Tenant>.Filter.In(x => x.Id, ids);
        return await db.Tenants.Find(filter).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<Tenant>> GetByCidsAsync(IReadOnlyCollection<int> cids, CancellationToken ct)
    {
        if (cids.Count == 0) return [];
        var filter = Builders<Tenant>.Filter.Eq(x => x.IsDeleted, false) & Builders<Tenant>.Filter.In(x => x.Cid, cids.Select(cid => (int?)cid));
        return await db.Tenants.Find(filter).SortByDescending(x => x.CreatedAt).ToListAsync(ct);
    }
    public async Task<IReadOnlyList<Tenant>> GetEvictedAsync(CancellationToken ct) =>
        await db.Tenants.Find(x => !x.IsDeleted && x.Status == TenantStatus.Evicted)
            .SortByDescending(x => x.EndDate).ToListAsync(ct);
    public Task CreateAsync(Tenant value, CancellationToken ct) { RepositoryHelpers.PrepareForInsert(value); return db.Tenants.InsertOneAsync(value, cancellationToken: ct); }
    public Task UpdateAsync(Tenant value, CancellationToken ct) => RepositoryHelpers.ReplaceAsync(db.Tenants, value, ct);
}

public sealed class UserRepository(MongoContext db) : IUserRepository
{
    public async Task<PagedResult<User>> QueryAsync(int page, int pageSize, ApprovalStatus? approval, AccessStatus? access, UserRole? role, CancellationToken ct)
    {
        page = Paging.NormalizePage(page); pageSize = Paging.NormalizePageSize(pageSize);
        var f = Builders<User>.Filter; var filter = f.Eq(x => x.IsDeleted, false);
        if (approval.HasValue) filter &= f.Eq(x => x.ApprovalStatus, approval.Value);
        if (access.HasValue) filter &= f.Eq(x => x.AccessStatus, access.Value);
        if (role.HasValue) filter &= f.Eq(x => x.Role, role.Value);
        var totalTask = db.Users.CountDocumentsAsync(filter, cancellationToken: ct);
        var itemsTask = db.Users.Find(filter).SortByDescending(x => x.CreatedAt).Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
        await Task.WhenAll(totalTask, itemsTask);
        return new(itemsTask.Result, page, pageSize, totalTask.Result);
    }
    public Task<User?> GetByIdAsync(string id, CancellationToken ct) => db.Users.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        await db.Users.Find(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync(ct);
    public async Task<IReadOnlyList<string>> SearchIdsAsync(string search, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(search)) return [];
        var regex = new MongoDB.Bson.BsonRegularExpression(
            System.Text.RegularExpressions.Regex.Escape(search.Trim()), "i");
        var f = Builders<User>.Filter;
        var filter = f.Regex(x => x.DisplayName, regex) |
                     f.Regex(x => x.Username, regex) |
                     f.Regex(x => x.FullName, regex);
        return await db.Users.Find(filter).Project(x => x.Id).Limit(100).ToListAsync(ct);
    }
    public Task<User?> GetByDiscordIdAsync(string id, CancellationToken ct) => db.Users.Find(x => x.DiscordUserId == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public Task<User?> GetByCidAsync(int cid, CancellationToken ct) => db.Users.Find(x => x.Cid == cid && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public Task<long> CountActiveManagersAsync(CancellationToken ct) => db.Users.CountDocumentsAsync(x => !x.IsDeleted && x.Role == UserRole.Manager && x.ApprovalStatus == ApprovalStatus.Approved && x.AccessStatus == AccessStatus.Active, cancellationToken: ct);
    public Task<long> CountPendingAsync(CancellationToken ct) => db.Users.CountDocumentsAsync(x => !x.IsDeleted && x.ApprovalStatus == ApprovalStatus.Pending, cancellationToken: ct);
    public Task CreateAsync(User value, CancellationToken ct) { RepositoryHelpers.PrepareForInsert(value); return db.Users.InsertOneAsync(value, cancellationToken: ct); }
    public Task UpdateAsync(User value, CancellationToken ct) => RepositoryHelpers.ReplaceAsync(db.Users, value, ct);
}
