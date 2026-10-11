using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Infrastructure.Persistence;
using MongoDB.Driver;

namespace ImperialEstates.Infrastructure.Repositories;

public sealed class BlockRepository(MongoContext db) : IBlockRepository
{
    public async Task<IReadOnlyList<Block>> GetAllAsync(bool activeOnly, CancellationToken ct)
    {
        var filter = Builders<Block>.Filter.Eq(x => x.IsDeleted, false);
        if (activeOnly) filter &= Builders<Block>.Filter.Eq(x => x.IsActive, true);
        return await db.Blocks.Find(filter).SortBy(x => x.BlockName).ToListAsync(ct);
    }
    public Task<Block?> GetByIdAsync(string id, CancellationToken ct) => db.Blocks.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public async Task<IReadOnlyList<Block>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        return await db.Blocks.Find(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync(ct);
    }
    public Task<Block?> GetByBusinessIdAsync(int id, CancellationToken ct) => db.Blocks.Find(x => x.BlockId == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public Task<Block?> GetByNameAsync(string name, CancellationToken ct) => db.Blocks.Find(x => x.BlockName == name && !x.IsDeleted).FirstOrDefaultAsync(ct)!;
    public Task CreateAsync(Block value, CancellationToken ct) { RepositoryHelpers.PrepareForInsert(value); return db.Blocks.InsertOneAsync(value, cancellationToken: ct); }
    public Task UpdateAsync(Block value, CancellationToken ct) => RepositoryHelpers.ReplaceAsync(db.Blocks, value, ct);
}

