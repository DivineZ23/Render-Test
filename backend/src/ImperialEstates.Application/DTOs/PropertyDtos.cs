using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Application.DTOs;

public sealed class PropertyQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 12;
    public string? Search { get; init; }
    public string? BlockId { get; init; }
    public PropertyType? Type { get; init; }
    public PropertyStatus? Status { get; init; }
    public decimal? MinRent { get; init; }
    public decimal? MaxRent { get; init; }
    public int? Bedrooms { get; init; }
    public int? PersonCapacity { get; init; }
    public int? StorageCapacity { get; init; }
    public string? Furnishing { get; init; }
    public string[] Amenities { get; init; } = [];
    public string SortBy { get; init; } = "newest";
    public string SortDirection { get; init; } = "desc";
}

public sealed record PropertyDto(
    string Id, int PropertyId, string BlockId, string BlockName, string PropertyName, string? Description,
    PropertyType Type, int? PersonCapacity, decimal? StateCost, int? StorageCapacity,
    string? Storage, decimal Rent, decimal? SecurityDeposit, PropertyStatus Status,
    int? Bedrooms, int? Bathrooms, int? Floor, decimal? Area, string? FurnishingStatus,
    IReadOnlyList<string> Amenities, IReadOnlyList<string> Images, string? CurrentTenantId,
    string? TenantName, int? TenantCid, string? TenantPhoneNumber, string? TenantDiscordId,
    DateTime? TenantStartDate, DateTime? TenantExpectedEndDate, decimal? TenantMonthlyRent,
    decimal? TenantSecurityDeposit, string? TenantEmergencyContact, string? TenantNotes,
    DateTime? RentPaidThrough,
    string? BookedByEnquiryId, int BookingCount, string? UnavailableReason, bool IsFeatured, bool IsActive,
    bool AllowOccupiedBookings,
    DateTime CreatedAt, DateTime UpdatedAt);

public sealed record PublicPropertyDto(
    string Id, int PropertyId, string BlockId, string BlockName, string PropertyName, string? Description,
    PropertyType Type, int? PersonCapacity, decimal? StateCost, int? StorageCapacity,
    string? Storage, decimal Rent, decimal? SecurityDeposit, PropertyStatus Status,
    int? Bedrooms, int? Bathrooms, int? Floor, decimal? Area, string? FurnishingStatus,
    IReadOnlyList<string> Amenities, IReadOnlyList<string> Images, bool IsFeatured, DateTime CreatedAt);

public sealed record UpsertPropertyRequest(
    int PropertyId, string BlockId, string PropertyName, string? Description, PropertyType Type,
    string? Storage, decimal Rent, decimal? SecurityDeposit, int? Bedrooms, int? Bathrooms,
    int? Floor, decimal? Area, string? FurnishingStatus, IReadOnlyList<string>? Amenities,
    IReadOnlyList<string>? Images, bool IsFeatured, bool IsActive = true, bool AllowOccupiedBookings = false);

public sealed record ChangePropertyStatusRequest(PropertyStatus Status, string? Reason, string? EnquiryId);

public sealed record AssignTenantRequest(
    int Cid, string FullName, string PhoneNumber, string DiscordId,
    DateTime StartDate, DateTime? ExpectedEndDate, decimal? MonthlyRent, decimal? SecurityDeposit,
    string? EmergencyContact, string? Notes);

public sealed record EvictTenantRequest(string? Reason, IReadOnlyList<string>? StorageImageUrls);

public sealed record CreatePropertyBookingRequest(
    int Cid, string FullName, string PhoneNumber, string DiscordId,
    decimal? MonthlyRent, decimal? BookingAmount, string? Notes);

public sealed record PropertyBookingDto(
    string Id, string PropertyId, int Cid, string FullName, string PhoneNumber,
    string DiscordId, decimal MonthlyRent, decimal BookingAmount, string? Notes,
    BookingStatus Status, string? CreatedByUserId,
    string? CreatedByDisplayName, DateTime CreatedAt);

public sealed record PropertyBookingGroupDto(
    string PropertyId, int PropertyNumber, string PropertyName, string BlockName,
    PropertyType Type, PropertyStatus Status, bool AllowOccupiedBookings,
    bool BookingAnnouncementPending, DateTime? BookingAnnouncementCreatedAt,
    DateTime? BookingAnnouncementPostedAt, string? BookingAnnouncementPostedByDisplayName,
    IReadOnlyList<PropertyBookingDto> Bookings)
{
    public IReadOnlyList<string> Images { get; init; } = [];
}

public sealed record BookingAnnouncementSummaryDto(long PendingCount);

public sealed record BookingAnnouncementStateDto(
    string PropertyId, bool IsPending, DateTime? CreatedAt, DateTime? PostedAt,
    string? PostedByDisplayName);

public sealed record SetBookingAnnouncementRequest(bool IsPosted);

public sealed record PropertyStatusHistoryDto(
    string Id, PropertyStatus PreviousStatus, PropertyStatus NewStatus, string? Reason,
    string ChangedByUserId, DateTime CreatedAt);
