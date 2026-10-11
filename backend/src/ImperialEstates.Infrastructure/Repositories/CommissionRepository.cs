using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Infrastructure.Persistence;
using MongoDB.Driver;

namespace ImperialEstates.Infrastructure.Repositories;

public sealed class CommissionRepository(MongoContext db) : ICommissionRepository
{
    public async Task<IReadOnlyList<CommissionRecord>> GetForAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct) =>
        await AnalyticsQuery(from, to).ToListAsync(ct);

    internal IFindFluent<CommissionRecord, CommissionRecord> AnalyticsQuery(DateTime? from, DateTime? to) =>
        db.Commissions.Find(x => !x.IsDeleted && x.SchemeVersion == CommissionRecord.CurrentSchemeVersion &&
            (((!from.HasValue || x.CreatedAt >= from.Value) && (!to.HasValue || x.CreatedAt <= to.Value)) ||
             (x.PaidAt.HasValue && (!from.HasValue || x.PaidAt >= from.Value) && (!to.HasValue || x.PaidAt <= to.Value))))
            .Project(x => new CommissionRecord
            {
                Id = x.Id, AgentUserId = x.AgentUserId, CommissionAmount = x.CommissionAmount,
                IsPaid = x.IsPaid, PaidAt = x.PaidAt, CreatedAt = x.CreatedAt,
                IsWinningAgent = x.IsWinningAgent, AuctionReference = x.AuctionReference,
            });

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

    public async Task<IReadOnlyList<CommissionRecord>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.Commissions.Find(x => ids.Contains(x.Id) && !x.IsDeleted &&
            x.SchemeVersion == CommissionRecord.CurrentSchemeVersion).ToListAsync(ct);

    public Task UpdateAsync(CommissionRecord value, CancellationToken ct) =>
        RepositoryHelpers.ReplaceAsync(db.Commissions, value, ct);
}
