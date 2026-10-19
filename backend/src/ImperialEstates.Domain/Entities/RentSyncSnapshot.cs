namespace ImperialEstates.Domain.Entities;

public sealed class RentSyncSnapshot : BaseDocument
{
    public IReadOnlyList<RentSyncRecord> Records { get; set; } = [];
    public string GoogleSheetSyncStatus { get; set; } = "notConfigured";
    public DateTime? GoogleSheetSyncedAt { get; set; }
    public string? GoogleSheetSyncError { get; set; }
    public string? GoogleSheetUrl { get; set; }
}

public sealed class RentSyncRecord
{
    public int RowNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidThrough { get; set; }
    public DateTime? LastPaid { get; set; }
    public DateTime? NextCharge { get; set; }
    public string Address { get; set; } = string.Empty;
    public string Interior { get; set; } = string.Empty;
    public int? Cid { get; set; }
    public string? RenterName { get; set; }
    public string? Phone { get; set; }
    public decimal Income { get; set; }
    public decimal Cost { get; set; }
    public string? TenantId { get; set; }
    public string? PropertyId { get; set; }
    public string? DiscordId { get; set; }
    public bool? NoticeGenerated { get; set; }
    public bool IsResolved { get; set; }
    public string? ResolvedByUserId { get; set; }
    public string? ResolvedByDisplayName { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsEvictionOnHold { get; set; }
    public string? EvictionHeldByUserId { get; set; }
    public string? EvictionHeldByDisplayName { get; set; }
    public DateTime? EvictionHeldAt { get; set; }
}
