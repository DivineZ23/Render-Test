using System.Linq;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Infrastructure.Persistence;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace ImperialEstates.Infrastructure.Repositories;

public sealed class RentSyncRepository(MongoContext db) : IRentSyncRepository
{
    public Task<RentRecordPage?> GetCurrentRecordsAsync(string status, int skip, int take, CancellationToken ct) =>
        CurrentRecordsQuery(status, skip, take).FirstOrDefaultAsync(ct)!;

    internal IFindFluent<RentSyncSnapshot, RentRecordPage> CurrentRecordsQuery(string status, int skip, int take) =>
        db.RentSyncSnapshots.Find(x => !x.IsDeleted).SortByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Project(x => new RentRecordPage(new RentSyncSnapshot
                { Id = x.Id, UpdatedAt = x.UpdatedAt,
                    Records = x.Records.Where(row => row.Status == status).OrderBy(row => row.RowNumber).Skip(skip).Take(take).ToList() },
                x.Records.Count, x.Records.Count(row => row.Status == "paid"),
                x.Records.Count(row => row.Status == "overdue"), x.Records.Count(row => row.Status == "evictable"),
                x.Records.Count(row => row.Status == "empty"), x.Records.Count(row => row.Status == status)));

    public async Task<IReadOnlyList<NoticeHistoryRow>> GetNoticePageAsync(string status, DateTime asOf, int skip, int take, CancellationToken ct) =>
        await NoticePageQuery(status, asOf, skip, take).ToListAsync(ct);

    internal IQueryable<NoticeHistoryRow> NoticePageQuery(string status, DateTime asOf, int skip, int take) =>
        db.RentSyncSnapshots.AsQueryable().Where(x => !x.IsDeleted && x.UpdatedAt <= asOf &&
                x.Records.Any(row => row.Status == status && row.NoticeGenerated != false))
            .OrderByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .SelectMany(x => x.Records.Where(row => row.Status == status && row.NoticeGenerated != false)
                .OrderBy(row => row.RowNumber), (snapshot, row) => new NoticeHistoryRow(snapshot.Id, snapshot.UpdatedAt, row))
            .Skip(skip).Take(take);

    public async Task<IReadOnlyList<NoticeSnapshotCounts>> GetNoticeCountsAsync(
        string status, DateTime dayStart, DateTime dayEnd, CancellationToken ct) =>
        await NoticeCountsQuery(status, dayStart, dayEnd).ToListAsync(ct);

    internal IFindFluent<RentSyncSnapshot, NoticeSnapshotCounts> NoticeCountsQuery(string status, DateTime dayStart, DateTime dayEnd) =>
        db.RentSyncSnapshots.Find(x => !x.IsDeleted).SortByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Project(x => new NoticeSnapshotCounts(x.UpdatedAt,
                x.Records.Count(row => row.Status == status),
                x.Records.Count(row => row.Status == status && row.NoticeGenerated != false),
                x.Records.Count(row => row.Status == status && row.NoticeGenerated != false && !row.IsResolved),
                x.Records.Count(row => row.Status == status && row.NoticeGenerated != false && row.IsResolved &&
                    row.ResolvedAt >= dayStart && row.ResolvedAt < dayEnd)));

    public async IAsyncEnumerable<RentSyncSnapshot> ReadEvictionHistoryAsync(DateTime asOf,
        IReadOnlyCollection<string> propertyIds, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var query = EvictionHistoryQuery(asOf, propertyIds);
        query.Options.BatchSize = 4;
        using var cursor = await query.ToCursorAsync(ct);
        while (await cursor.MoveNextAsync(ct))
            foreach (var snapshot in cursor.Current)
                yield return snapshot;
    }

    public Task<RentSyncSnapshot?> GetCurrentAsync(CancellationToken ct) =>
        db.RentSyncSnapshots.Find(x => !x.IsDeleted).SortByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(ct)!;

    public async Task<decimal?> GetLatestIncomeAsync(CancellationToken ct)
    {
        var result = await IncomeQuery().FirstOrDefaultAsync(ct);
        return result?.Income;
    }

    internal IFindFluent<RentSyncSnapshot, RentSyncIncome> IncomeQuery() =>
        db.RentSyncSnapshots.Find(x => !x.IsDeleted).SortByDescending(x => x.UpdatedAt)
            .Project(x => new RentSyncIncome(x.Records.Sum(record => record.Income)));

    internal sealed record RentSyncIncome(decimal Income);

    public Task<RentSyncSnapshot?> GetByIdAsync(string id, CancellationToken ct) =>
        db.RentSyncSnapshots.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync(ct)!;

    public async Task<IReadOnlyList<RentSyncSnapshot>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : await db.RentSyncSnapshots.Find(x => ids.Contains(x.Id) && !x.IsDeleted).ToListAsync(ct);

    public async Task<IReadOnlyList<RentSyncSnapshot>> GetAllAsync(CancellationToken ct) =>
        await db.RentSyncSnapshots.Find(x => !x.IsDeleted).SortByDescending(x => x.UpdatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<RentSyncSnapshotDto>> GetSummariesAsync(CancellationToken ct) =>
        await SummaryQuery().ToListAsync(ct);

    internal IFindFluent<RentSyncSnapshot, RentSyncSnapshotDto> SummaryQuery() =>
        db.RentSyncSnapshots.Find(x => !x.IsDeleted).SortByDescending(x => x.UpdatedAt)
            .Project(value => new RentSyncSnapshotDto(
                value.Id, value.CreatedBy, value.UpdatedAt, value.Records.Count,
                value.Records.Count(record => record.Status == "paid"),
                value.Records.Count(record => record.Status == "overdue"),
                value.Records.Count(record => record.Status == "evictable"),
                value.Records.Count(record => record.Status == "empty"),
                value.Records.Count(record => record.Cid.HasValue && (record.DiscordId == null || record.DiscordId == "")),
                value.GoogleSheetSyncStatus, value.GoogleSheetSyncedAt, value.GoogleSheetSyncError,
                value.GoogleSheetUrl, Array.Empty<RentSyncRecordDto>()));

    public async Task<IReadOnlyList<RentSyncSnapshot>> GetEvictionHistoryAsync(
        DateTime asOf, IReadOnlyCollection<string> propertyIds, CancellationToken ct) =>
        await EvictionHistoryQuery(asOf, propertyIds).ToListAsync(ct);

    internal IFindFluent<RentSyncSnapshot, RentSyncSnapshot> EvictionHistoryQuery(
        DateTime asOf, IReadOnlyCollection<string> propertyIds) =>
        db.RentSyncSnapshots.Find(x => !x.IsDeleted && x.UpdatedAt <= asOf &&
            x.Records.Any(record => (propertyIds.Count == 0 || propertyIds.Contains(record.PropertyId!) ||
                record.PropertyId == null || record.PropertyId == "") &&
                (record.Status != "evictable" || record.NoticeGenerated != false)))
            .SortByDescending(x => x.UpdatedAt).ThenByDescending(x => x.Id)
            .Project(x => new RentSyncSnapshot
            {
                Id = x.Id,
                Records = x.Records.Where(record => (propertyIds.Count == 0 || propertyIds.Contains(record.PropertyId!) ||
                    record.PropertyId == null || record.PropertyId == "") &&
                    (record.Status != "evictable" || record.NoticeGenerated != false))
                    .Select(record => new RentSyncRecord
                    {
                        PropertyId = record.PropertyId, Address = record.Address, Status = record.Status,
                        RowNumber = record.RowNumber, NoticeGenerated = record.NoticeGenerated,
                        IsResolved = record.IsResolved, ResolvedAt = record.ResolvedAt,
                        IsEvictionOnHold = record.IsEvictionOnHold,
                        EvictionHeldByUserId = record.EvictionHeldByUserId,
                        EvictionHeldByDisplayName = record.EvictionHeldByDisplayName,
                        EvictionHeldAt = record.EvictionHeldAt,
                    }).ToList(),
            });

    public async Task<IReadOnlyList<RentSyncSnapshot>> GetNoticesForAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct) =>
        await AnalyticsQuery(from, to).ToListAsync(ct);

    internal IFindFluent<RentSyncSnapshot, RentSyncSnapshot> AnalyticsQuery(DateTime? from, DateTime? to) =>
        db.RentSyncSnapshots.Find(x => !x.IsDeleted && x.Records.Any(record =>
            (record.Status == "overdue" || record.Status == "evictable") && record.ResolvedAt.HasValue &&
            (!from.HasValue || record.ResolvedAt >= from.Value) && (!to.HasValue || record.ResolvedAt <= to.Value)))
            .Project(x => new RentSyncSnapshot
            {
                Id = x.Id,
                Records = x.Records.Where(record =>
                    (record.Status == "overdue" || record.Status == "evictable") && record.ResolvedAt.HasValue &&
                    (!from.HasValue || record.ResolvedAt >= from.Value) && (!to.HasValue || record.ResolvedAt <= to.Value))
                    .Select(record => new RentSyncRecord
                    {
                        RowNumber = record.RowNumber, Status = record.Status, Address = record.Address,
                        RenterName = record.RenterName, Cid = record.Cid, Income = record.Income,
                        ResolvedAt = record.ResolvedAt, ResolvedByUserId = record.ResolvedByUserId,
                    }).ToList(),
            });

    public async Task SaveCurrentAsync(RentSyncSnapshot snapshot, CancellationToken ct)
    {
        RepositoryHelpers.PrepareForInsert(snapshot);
        await db.RentSyncSnapshots.InsertOneAsync(snapshot, cancellationToken: ct);
    }

    public Task UpdateAsync(RentSyncSnapshot snapshot, CancellationToken ct) =>
        db.RentSyncSnapshots.ReplaceOneAsync(x => x.Id == snapshot.Id, snapshot, cancellationToken: ct);

    public async Task DeleteAsync(string id, CancellationToken ct)
    {
        await db.RentSyncSnapshots.DeleteOneAsync(x => x.Id == id, cancellationToken: ct);
    }
}
