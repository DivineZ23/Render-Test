using ImperialEstates.Application.Common;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Infrastructure.Persistence;
using MongoDB.Driver;

namespace ImperialEstates.Infrastructure.Repositories;

public sealed class RecruitmentApplicationRepository(MongoContext db) : IRecruitmentApplicationRepository
{
    public async Task<PagedResult<RecruitmentApplication>> QueryAsync(
        int page,
        int pageSize,
        RecruitmentStatus? status,
        CancellationToken ct)
    {
        var f = Builders<RecruitmentApplication>.Filter;
        var filter = f.Eq(x => x.IsDeleted, false);
        if (status.HasValue) filter &= f.Eq(x => x.Status, status.Value);

        var totalTask = db.RecruitmentApplications.CountDocumentsAsync(filter, cancellationToken: ct);
        var itemsTask = db.RecruitmentApplications
            .Find(filter)
            .SortByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(ct);
        await Task.WhenAll(totalTask, itemsTask);
        return new(itemsTask.Result, page, pageSize, totalTask.Result);
    }

    public Task<RecruitmentApplication?> GetByIdAsync(string id, CancellationToken ct) =>
        db.RecruitmentApplications.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<RecruitmentApplication>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.RecruitmentApplications.Find(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync(ct);

    public async Task<bool> HasPendingAsync(int characterCid, string discordId, CancellationToken ct) =>
        await db.RecruitmentApplications.CountDocumentsAsync(
            x =>
                !x.IsDeleted &&
                x.Status == RecruitmentStatus.Pending &&
                (x.CharacterCid == characterCid || x.DiscordId == discordId),
            cancellationToken: ct) > 0;

    public Task CreateAsync(RecruitmentApplication application, CancellationToken ct)
    {
        RepositoryHelpers.PrepareForInsert(application);
        return db.RecruitmentApplications.InsertOneAsync(application, cancellationToken: ct);
    }

    public Task UpdateAsync(RecruitmentApplication application, CancellationToken ct) =>
        RepositoryHelpers.ReplaceAsync(db.RecruitmentApplications, application, ct);
}
