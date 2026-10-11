using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Application.Interfaces;

public interface IBlockRepository
{
    Task<IReadOnlyList<Block>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<Block?> GetByIdAsync(string id, CancellationToken cancellationToken);
    async Task<IReadOnlyList<Block>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        (await GetAllAsync(false, ct)).Where(value => ids.Contains(value.Id)).ToList();
    Task<Block?> GetByBusinessIdAsync(int blockId, CancellationToken cancellationToken);
    Task<Block?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task CreateAsync(Block block, CancellationToken cancellationToken);
    Task UpdateAsync(Block block, CancellationToken cancellationToken);
}

public interface IPropertyRepository
{
    async Task<IReadOnlyList<EvictionPropertyLookup>> GetEvictionCandidatesAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        (await (ids.Count == 0 ? GetAllAsync(ct) : GetByIdsAsync(ids, ct))).Select(value =>
            new EvictionPropertyLookup(value.Id, value.PropertyId, value.PropertyName, value.Status, value.CurrentTenantId)).ToList();
    Task<PagedResult<Property>> QueryAsync(PropertyQuery query, bool publicOnly, CancellationToken cancellationToken);
    Task<Property?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Property>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);
    Task<Property?> GetByBusinessIdAsync(int propertyId, CancellationToken cancellationToken);
    Task<Property?> GetByNameAsync(string propertyName, CancellationToken cancellationToken);
    Task<IReadOnlyList<Property>> GetAllAsync(CancellationToken cancellationToken);
    async Task<IReadOnlyList<PropertyPortfolioItem>> GetPortfolioAsync(CancellationToken ct) =>
        (await GetAllAsync(ct)).Select(value => new PropertyPortfolioItem(
            value.BlockId, value.Type, value.Rent, value.Status, value.CurrentTenantId)).ToList();
    Task<IReadOnlyList<Property>> GetFeaturedAsync(int limit, CancellationToken cancellationToken);
    Task<long> CountByBlockAsync(string blockId, CancellationToken cancellationToken);
    Task<long> CountByStatusAsync(PropertyStatus? status, CancellationToken cancellationToken);
    async Task<long> CountPendingBookingAnnouncementsAsync(CancellationToken cancellationToken) =>
        (await GetAllAsync(cancellationToken)).LongCount(property =>
            !property.IsDeleted && property.IsActive && property.BookingAnnouncementPending);
    Task CreateAsync(Property property, CancellationToken cancellationToken);
    Task UpdateAsync(Property property, CancellationToken cancellationToken);
}

public interface IPropertyBookingRepository
{
    Task<IReadOnlyList<PropertyBooking>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PropertyBooking>> GetForAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct) => GetAllAsync(ct);
    Task<IReadOnlyList<PropertyBooking>> GetAllActiveAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<PropertyBooking>> GetActiveByPropertyAsync(string propertyId, CancellationToken cancellationToken);
    Task<PropertyBooking?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<long> CountActiveByPropertyAsync(string propertyId, CancellationToken cancellationToken);
    async Task<IReadOnlyDictionary<string, long>> CountActiveByPropertiesAsync(
        IReadOnlyCollection<string> propertyIds,
        CancellationToken cancellationToken)
    {
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var propertyId in propertyIds)
            counts[propertyId] = await CountActiveByPropertyAsync(propertyId, cancellationToken);
        return counts;
    }
    Task CreateAsync(PropertyBooking booking, CancellationToken cancellationToken);
    Task UpdateAsync(PropertyBooking booking, CancellationToken cancellationToken);
    Task CloseActiveAsync(string propertyId, BookingStatus status, string actorId, CancellationToken cancellationToken);
}

public interface ITenantRepository
{
    Task<PagedResult<Tenant>> QueryAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Tenant>> GetForAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct) => GetAllAsync(ct);
    Task<Tenant?> GetByIdAsync(string id, CancellationToken cancellationToken);
    async Task<IReadOnlyList<Tenant>> GetByIdsAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        var values = new List<Tenant>();
        foreach (var id in ids)
        {
            var value = await GetByIdAsync(id, cancellationToken);
            if (value is not null) values.Add(value);
        }
        return values;
    }
    Task<IReadOnlyList<Tenant>> GetByCidsAsync(IReadOnlyCollection<int> cids, CancellationToken cancellationToken);
    Task<IReadOnlyList<Tenant>> GetNoticeMappingsAsync(IReadOnlyCollection<int> cids, CancellationToken ct) => GetByCidsAsync(cids, ct);
    Task<IReadOnlyList<Tenant>> GetEvictedAsync(CancellationToken cancellationToken);
    Task CreateAsync(Tenant tenant, CancellationToken cancellationToken);
    Task UpdateAsync(Tenant tenant, CancellationToken cancellationToken);
}

public interface IRentSyncRepository
{
    async Task<RentRecordPage?> GetCurrentRecordsAsync(string status, int skip, int take, CancellationToken ct)
    {
        var value = await GetCurrentAsync(ct);
        if (value is null) return null;
        return new(new RentSyncSnapshot { Id = value.Id, UpdatedAt = value.UpdatedAt,
                Records = value.Records.Where(row => row.Status == status).OrderBy(row => row.RowNumber).Skip(skip).Take(take).ToList() },
            value.Records.Count, value.Records.Count(row => row.Status == "paid"),
            value.Records.Count(row => row.Status == "overdue"), value.Records.Count(row => row.Status == "evictable"),
            value.Records.Count(row => row.Status == "empty"), value.Records.Count(row => row.Status == status));
    }
    async Task<IReadOnlyList<NoticeHistoryRow>> GetNoticePageAsync(string status, DateTime asOf, int skip, int take, CancellationToken ct) =>
        (await GetAllAsync(ct)).Where(snapshot => snapshot.UpdatedAt <= asOf).SelectMany(snapshot => snapshot.Records
            .Where(row => row.Status == status && row.NoticeGenerated != false).OrderBy(row => row.RowNumber)
            .Select(row => new NoticeHistoryRow(snapshot.Id, snapshot.UpdatedAt, row))).Skip(skip).Take(take).ToList();
    async Task<IReadOnlyList<NoticeSnapshotCounts>> GetNoticeCountsAsync(string status, DateTime dayStart, DateTime dayEnd, CancellationToken ct) =>
        (await GetAllAsync(ct)).Select(snapshot => new NoticeSnapshotCounts(snapshot.UpdatedAt,
            snapshot.Records.Count(row => row.Status == status),
            snapshot.Records.Count(row => row.Status == status && row.NoticeGenerated != false),
            snapshot.Records.Count(row => row.Status == status && row.NoticeGenerated != false && !row.IsResolved),
            snapshot.Records.Count(row => row.Status == status && row.NoticeGenerated != false && row.IsResolved &&
                row.ResolvedAt >= dayStart && row.ResolvedAt < dayEnd))).ToList();
    async IAsyncEnumerable<RentSyncSnapshot> ReadEvictionHistoryAsync(DateTime asOf, IReadOnlyCollection<string> propertyIds,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var snapshot in await GetEvictionHistoryAsync(asOf, propertyIds, ct))
        {
            ct.ThrowIfCancellationRequested();
            yield return snapshot;
        }
    }
    Task<RentSyncSnapshot?> GetCurrentAsync(CancellationToken cancellationToken);
    async Task<decimal?> GetLatestIncomeAsync(CancellationToken ct) =>
        (await GetCurrentAsync(ct))?.Records.Sum(record => record.Income);
    Task<RentSyncSnapshot?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<RentSyncSnapshot>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        RepositoryBatch.FetchAsync(ids, GetByIdAsync, ct);
    Task<IReadOnlyList<RentSyncSnapshot>> GetAllAsync(CancellationToken cancellationToken);
    async Task<IReadOnlyList<RentSyncSnapshotDto>> GetSummariesAsync(CancellationToken ct) =>
        (await GetAllAsync(ct)).Select(value => new RentSyncSnapshotDto(
            value.Id, value.CreatedBy, value.UpdatedAt, value.Records.Count,
            value.Records.Count(record => record.Status == "paid"),
            value.Records.Count(record => record.Status == "overdue"),
            value.Records.Count(record => record.Status == "evictable"),
            value.Records.Count(record => record.Status == "empty"),
            value.Records.Count(record => record.Cid.HasValue && string.IsNullOrEmpty(record.DiscordId)),
            value.GoogleSheetSyncStatus, value.GoogleSheetSyncedAt, value.GoogleSheetSyncError,
            value.GoogleSheetUrl, Array.Empty<RentSyncRecordDto>())).ToList();
    Task<IReadOnlyList<RentSyncSnapshot>> GetEvictionHistoryAsync(DateTime asOf, IReadOnlyCollection<string> propertyIds, CancellationToken ct) => GetAllAsync(ct);
    Task<IReadOnlyList<RentSyncSnapshot>> GetNoticesForAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct) => GetAllAsync(ct);
    Task SaveCurrentAsync(RentSyncSnapshot snapshot, CancellationToken cancellationToken);
    Task UpdateAsync(RentSyncSnapshot snapshot, CancellationToken cancellationToken);
    Task DeleteAsync(string id, CancellationToken cancellationToken);
}

public interface IUserRepository
{
    Task<PagedResult<User>> QueryAsync(int page, int pageSize, ApprovalStatus? approval, AccessStatus? access, UserRole? role, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken);
    async Task<IReadOnlyList<User>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken)
    {
        var values = new List<User>();
        foreach (var id in ids)
        {
            var value = await GetByIdAsync(id, cancellationToken);
            if (value is not null) values.Add(value);
        }
        return values;
    }
    Task<User?> GetByDiscordIdAsync(string discordId, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> SearchIdsAsync(string search, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>([]);
    Task<User?> GetByCidAsync(int cid, CancellationToken cancellationToken);
    Task<long> CountActiveManagersAsync(CancellationToken cancellationToken);
    Task<long> CountPendingAsync(CancellationToken cancellationToken);
    Task CreateAsync(User user, CancellationToken cancellationToken);
    Task UpdateAsync(User user, CancellationToken cancellationToken);
}

public interface IOwnerIdentity
{
    bool IsOwner(string discordUserId);
}

public interface IEnquiryRepository
{
    Task<PagedResult<Enquiry>> QueryAsync(int page, int pageSize, EnquiryStatus? status, CancellationToken cancellationToken);
    Task<Enquiry?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Enquiry>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        RepositoryBatch.FetchAsync(ids, GetByIdAsync, ct);
    Task<long> CountPendingAsync(CancellationToken cancellationToken);
    Task CreateAsync(Enquiry enquiry, CancellationToken cancellationToken);
    Task UpdateAsync(Enquiry enquiry, CancellationToken cancellationToken);
}

public interface IAuditRepository
{
    Task CreateAsync(AuditLog auditLog, CancellationToken cancellationToken);
    Task<PagedResult<AuditLog>> QueryAsync(int page, int pageSize, CancellationToken cancellationToken);
    Task<PagedResult<AuditLog>> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken) =>
        QueryAsync(query.Page, query.PageSize, cancellationToken);
}

public sealed record PropertyPortfolioItem(
    string BlockId, PropertyType Type, decimal Rent, PropertyStatus Status, string? CurrentTenantId);

public interface IRecruitmentApplicationRepository
{
    Task<PagedResult<RecruitmentApplication>> QueryAsync(int page, int pageSize, RecruitmentStatus? status, CancellationToken cancellationToken);
    Task<RecruitmentApplication?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<RecruitmentApplication>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        RepositoryBatch.FetchAsync(ids, GetByIdAsync, ct);
    Task<bool> HasPendingAsync(int characterCid, string discordId, CancellationToken cancellationToken);
    Task CreateAsync(RecruitmentApplication application, CancellationToken cancellationToken);
    Task UpdateAsync(RecruitmentApplication application, CancellationToken cancellationToken);
}

public interface IStatusHistoryRepository
{
    Task CreateAsync(PropertyStatusHistory history, CancellationToken cancellationToken);
    Task<IReadOnlyList<PropertyStatusHistory>> GetByPropertyAsync(string propertyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PropertyStatusHistory>> GetRecentAsync(int limit, CancellationToken cancellationToken);
}

public interface IPropertyLifecycleStore
{
    Task AssignTenantAsync(Property property, Tenant tenant, PropertyStatusHistory history, AuditLog audit, CommissionRecord? commission, CancellationToken cancellationToken);
    Task UpdateTenantAsync(Property property, Tenant tenant, AuditLog audit, CancellationToken cancellationToken);
    Task EvictAsync(Property property, Tenant tenant, PropertyStatusHistory history, AuditLog audit, CancellationToken cancellationToken);
}

public interface ICommissionRepository
{
    Task CreateManyAsync(IReadOnlyCollection<CommissionRecord> values, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommissionRecord>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CommissionRecord>> GetForAnalyticsAsync(DateTime? from, DateTime? to, CancellationToken ct) => GetAllAsync(ct);
    Task<IReadOnlyList<CommissionRecord>> GetByAgentAsync(string userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommissionRecord>> GetBySettlementIdAsync(string settlementId, CancellationToken cancellationToken);
    Task<CommissionRecord?> GetByIdAsync(string id, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommissionRecord>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) =>
        RepositoryBatch.FetchAsync(ids, GetByIdAsync, ct);
    Task UpdateAsync(CommissionRecord commission, CancellationToken cancellationToken);
}

public interface ISettingRepository
{
    Task<ApplicationSetting?> GetAsync(string key, CancellationToken cancellationToken);
    Task UpsertAsync(ApplicationSetting setting, CancellationToken cancellationToken);
}

internal static class RepositoryBatch
{
    public static async Task<IReadOnlyList<T>> FetchAsync<T>(IReadOnlyCollection<string> ids,
        Func<string, CancellationToken, Task<T?>> fetch, CancellationToken ct) where T : class =>
        (await Task.WhenAll(ids.Distinct().Select(id => fetch(id, ct)))).OfType<T>().ToList();
}
