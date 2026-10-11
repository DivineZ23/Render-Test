using ImperialEstates.Api.Extensions;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperialEstates.Api.Controllers;

[ApiController, Route("api/v1/notices")]
public sealed class NoticesController(RentSyncService service) : ControllerBase
{
    [Authorize(Policy = "ApprovedUser"), HttpGet("feed")]
    public Task<NoticeFeedDto> Feed(CancellationToken ct, [FromQuery] string status = "evictable",
        [FromQuery] int skip = 0, [FromQuery] int take = 50, [FromQuery] DateTime? asOf = null) =>
        service.GetNoticeFeedAsync(status, skip, take, asOf, ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("feed-summary")]
    public Task<NoticeFeedSummaryDto> FeedSummary(CancellationToken ct, [FromQuery] string status = "evictable", [FromQuery] DateTime? dayStart = null,
        [FromQuery] DateTime? dayEnd = null) =>
        service.GetNoticeSummaryAsync(status, dayStart ?? DateTime.UtcNow.Date,
            dayEnd ?? (dayStart ?? DateTime.UtcNow.Date).AddDays(1), ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("records")]
    public Task<RentRecordPageDto> Records(CancellationToken ct, [FromQuery] string status = "overdue",
        [FromQuery] int skip = 0, [FromQuery] int take = 50) => service.GetCurrentRecordsAsync(status, skip, take, ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("snapshot")]
    public Task<RentSyncSnapshotDto> Snapshot(CancellationToken ct) => service.GetCurrentAsync(ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("snapshots")]
    public Task<IReadOnlyList<RentSyncSnapshotDto>> Snapshots(CancellationToken ct, [FromQuery] bool summaryOnly = false) =>
        summaryOnly ? service.GetSummariesAsync(ct) : service.GetAllAsync(ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("eviction-queue")]
    public Task<IReadOnlyList<EvictionQueueItemDto>> EvictionQueue(CancellationToken ct) =>
        service.GetEvictionQueueAsync(ct);

    [Authorize(Policy = "Manager"), HttpPost("sync")]
    public Task<RentSyncSnapshotDto> Sync(RentSyncRequest request, CancellationToken ct) =>
        service.SyncAsync(request, User.UserId(), ct);

    [Authorize(Policy = "Manager"), HttpPost("sync/google-sheet/retry")]
    public Task<RentSyncSnapshotDto> RetryGoogleSheetSync(CancellationToken ct) =>
        service.RetryGoogleSheetSyncAsync(User.UserId(), ct);

    [Authorize(Policy = "Manager"), HttpDelete("snapshots/{id}")]
    public Task Delete(string id, CancellationToken ct) => service.DeleteAsync(id, User.UserId(), ct);

    [Authorize(Policy = "ApprovedUser"), HttpPatch("snapshots/{snapshotId}/records/{rowNumber:int}/resolution")]
    public Task<RentSyncSnapshotDto> SetResolution(string snapshotId, int rowNumber, SetNoticeResolutionRequest request, CancellationToken ct) =>
        service.SetResolutionAsync(snapshotId, rowNumber, request.IsResolved, User.UserId(), ct);

    [Authorize(Policy = "ApprovedUser"), HttpPatch("snapshots/{snapshotId}/records/{rowNumber:int}/resolution-record")]
    public Task<RentSyncRecordDto> SetResolutionRecord(string snapshotId, int rowNumber, SetNoticeResolutionRequest request, CancellationToken ct) =>
        service.SetResolutionRecordAsync(snapshotId, rowNumber, request.IsResolved, User.UserId(), ct);

    [Authorize(Policy = "Manager"), HttpPatch("eviction-queue/{snapshotId}/records/{rowNumber:int}/hold")]
    public async Task<IActionResult> SetEvictionHold(
        string snapshotId, int rowNumber, SetEvictionHoldRequest request, CancellationToken ct)
    {
        await service.SetEvictionHoldAsync(snapshotId, rowNumber, request.IsOnHold, User.UserId(), ct);
        return NoContent();
    }
}
