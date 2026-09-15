using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Infrastructure.Persistence;
using MongoDB.Driver;

namespace ImperialEstates.Infrastructure.Repositories;

public sealed class CommissionRepository(MongoContext db) : ICommissionRepository
{
    public async Task CreateManyAsync(IReadOnlyCollection<CommissionRecord> values, CancellationToken ct)
    {
        foreach (var value in values) RepositoryHelpers.PrepareForInsert(value);
        if (values.Count > 0) await db.Commissions.InsertManyAsync(values, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<CommissionRecord>> GetAllAsync(CancellationToken ct) =>
        await db.Commissions.Find(x => !x.IsDeleted && x.SchemeVersion == CommissionRecord.CurrentSchemeVersion)
            .SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<CommissionRecord>> GetByAgentAsync(string userId, CancellationToken ct) =>
        await db.Commissions.Find(x => !x.IsDeleted && x.SchemeVersion == CommissionRecord.CurrentSchemeVersion && x.AgentUserId == userId)
            .SortByDescending(x => x.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<CommissionRecord>> GetBySettlementIdAsync(string settlementId, CancellationToken ct) =>
        await db.Commissions.Find(x => !x.IsDeleted && x.SchemeVersion == CommissionRecord.CurrentSchemeVersion && x.SettlementId == settlementId)
            .SortBy(x => x.CreatedAt).ToListAsync(ct);

    public Task<CommissionRecord?> GetByIdAsync(string id, CancellationToken ct) =>
        db.Commissions.Find(x => x.Id == id && !x.IsDeleted && x.SchemeVersion == CommissionRecord.CurrentSchemeVersion).FirstOrDefaultAsync(ct)!;

    public Task UpdateAsync(CommissionRecord value, CancellationToken ct) =>
        RepositoryHelpers.ReplaceAsync(db.Commissions, value, ct);
}
