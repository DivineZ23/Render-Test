using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Application.Services;

public sealed class PropertyService(
    IPropertyRepository properties, IBlockRepository blocks, ITenantRepository tenants,
    IUserRepository users, IStatusHistoryRepository history, IAuditRepository audits,
    IPropertyLifecycleStore lifecycle,
    IPropertyBookingRepository bookings)
{
    public async Task<PagedResult<PublicPropertyDto>> GetAvailableAsync(PropertyQuery query, CancellationToken cancellationToken)
    {
        var values = await properties.QueryAsync(query, true, cancellationToken);
        var names = await BlockNamesAsync(values.Items, cancellationToken);
        return new PagedResult<PublicPropertyDto>(values.Items.Select(p => p.ToPublicDto(names[p.BlockId])).ToList(), values.Page, values.PageSize, values.TotalItems);
    }

    public async Task<IReadOnlyList<PublicPropertyDto>> GetFeaturedAsync(CancellationToken cancellationToken)
    {
        var values = await properties.GetFeaturedAsync(6, cancellationToken);
        var names = await BlockNamesAsync(values, cancellationToken);
        return values.Select(p => p.ToPublicDto(names[p.BlockId])).ToList();
    }

    public async Task<PagedResult<PropertyDto>> GetAllAsync(PropertyQuery query, CancellationToken cancellationToken)
    {
        var values = await properties.QueryAsync(query, false, cancellationToken);
        var tenantIds = values.Items
            .Select(property => property.CurrentTenantId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var propertyIds = values.Items.Select(property => property.Id).ToArray();
        var namesTask = BlockNamesAsync(values.Items, cancellationToken);
        var tenantsTask = tenants.GetByIdsAsync(tenantIds, cancellationToken);
        var bookingCountsTask = bookings.CountActiveByPropertiesAsync(propertyIds, cancellationToken);
        await Task.WhenAll(namesTask, tenantsTask, bookingCountsTask);

        var names = namesTask.Result;
        var tenantsById = tenantsTask.Result.ToDictionary(tenant => tenant.Id, StringComparer.Ordinal);
        var bookingCounts = bookingCountsTask.Result;
        var items = values.Items.Select(property =>
        {
            var tenant = !string.IsNullOrWhiteSpace(property.CurrentTenantId) &&
                tenantsById.TryGetValue(property.CurrentTenantId, out var currentTenant)
                    ? currentTenant
                    : null;
            var bookingCount = bookingCounts.TryGetValue(property.Id, out var count) ? count : 0;
            return property.ToDto(names[property.BlockId], tenant, checked((int)bookingCount));
        }).ToList();
        return new PagedResult<PropertyDto>(items, values.Page, values.PageSize, values.TotalItems);
    }

    public async Task<PublicPropertyDto> GetPublicAsync(string id, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        if (!value.IsActive || value.IsDeleted || value.Status != PropertyStatus.Available)
            throw new KeyNotFoundException("Property not found.");
        var block = await GetBlockAsync(value.BlockId, cancellationToken);
        return value.ToPublicDto(block.BlockName);
    }

    public async Task<PropertyDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        return await ToManagementDtoAsync(value, (await GetBlockAsync(value.BlockId, cancellationToken)).BlockName, cancellationToken);
    }

    public async Task<PropertyDto> CreateAsync(UpsertPropertyRequest request, string actorId, CancellationToken cancellationToken)
    {
        var block = await GetBlockAsync(request.BlockId, cancellationToken);
        if (!block.IsActive || await properties.GetByBusinessIdAsync(request.PropertyId, cancellationToken) is not null)
            throw new DomainRuleException("The property ID must be unique and the block must be active.", "PROPERTY_NOT_UNIQUE_OR_BLOCK_INACTIVE");
        var value = Map(request, new Property { CreatedBy = actorId });
        await properties.CreateAsync(value, cancellationToken);
        await audits.CreateAsync(Audit("property.created", value.Id, actorId), cancellationToken);
        return value.ToDto(block.BlockName);
    }

    public async Task<PropertyDto> UpdateAsync(string id, UpsertPropertyRequest request, string actorId, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        var block = await GetBlockAsync(request.BlockId, cancellationToken);
        var duplicate = await properties.GetByBusinessIdAsync(request.PropertyId, cancellationToken);
        if ((duplicate is not null && duplicate.Id != id) || !block.IsActive)
            throw new DomainRuleException("The property ID must be unique and the block must be active.", "PROPERTY_NOT_UNIQUE_OR_BLOCK_INACTIVE");
        Map(request, value);
        value.UpdatedBy = actorId;
        value.UpdatedAt = DateTime.UtcNow;
        await properties.UpdateAsync(value, cancellationToken);
        await audits.CreateAsync(Audit("property.updated", value.Id, actorId), cancellationToken);
        return value.ToDto(block.BlockName);
    }

    public async Task<PropertyDto> ChangeStatusAsync(string id, ChangePropertyStatusRequest request, string actorId, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        var isManagerControlledTransition = request.Status is PropertyStatus.Auction or PropertyStatus.OnHold ||
            request.Status == PropertyStatus.Available && value.Status is PropertyStatus.Auction or PropertyStatus.OnHold;
        if (isManagerControlledTransition)
        {
            var actor = await users.GetByIdAsync(actorId, cancellationToken) ?? throw new UnauthorizedAccessException();
            if (actor.Role is not (UserRole.Manager or UserRole.Owner))
                throw new UnauthorizedAccessException("Only managers and owners can list a property for auction or place it on hold.");
        }
        var previous = value.Status;
        switch (request.Status)
        {
            case PropertyStatus.Available:
                value.MakeAvailable();
                if (previous == PropertyStatus.Booked)
                {
                    await bookings.CloseActiveAsync(value.Id, BookingStatus.Cancelled, actorId, cancellationToken);
                    value.ClearBookingAnnouncement();
                }
                break;
            case PropertyStatus.Booked:
                throw new DomainRuleException("Use the booking form to book a property.", "BOOKING_FORM_REQUIRED");
            case PropertyStatus.Auction:
                value.MarkForAuction();
                if (previous == PropertyStatus.Booked)
                {
                    await bookings.CloseActiveAsync(value.Id, BookingStatus.Cancelled, actorId, cancellationToken);
                    value.ClearBookingAnnouncement();
                }
                break;
            case PropertyStatus.OnHold:
                value.PlaceOnHold(request.Reason ?? string.Empty);
                if (previous == PropertyStatus.Booked)
                {
                    await bookings.CloseActiveAsync(value.Id, BookingStatus.Cancelled, actorId, cancellationToken);
                    value.ClearBookingAnnouncement();
                }
                break;
            case PropertyStatus.Paid:
            case PropertyStatus.Overdue:
            case PropertyStatus.Evictable:
                throw new DomainRuleException("Rental statuses are controlled by tenant assignment and rent data sync.", "RENTAL_STATUS_MANAGED");
        }
        value.UpdatedBy = actorId;
        await properties.UpdateAsync(value, cancellationToken);
        await RecordStatusAsync(value, previous, request.Reason, actorId, "property.status.changed", cancellationToken);
        return value.ToDto((await GetBlockAsync(value.BlockId, cancellationToken)).BlockName);
    }

    public async Task<PropertyDto> AssignTenantAsync(string id, AssignTenantRequest request, string actorId, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        var previous = value.Status;
        value.Rent = request.MonthlyRent!.Value;
        var tenant = new Tenant
        {
            PropertyId = value.Id, FullName = request.FullName.Trim(), PhoneNumber = request.PhoneNumber.Trim(),
            Cid = request.Cid, DiscordId = request.DiscordId.Trim(),
            StartDate = request.StartDate, ExpectedEndDate = request.ExpectedEndDate, MonthlyRent = request.MonthlyRent.Value,
            RentPaidThrough = request.StartDate.Date.AddDays(7),
            SecurityDeposit = request.SecurityDeposit ?? 0, EmergencyContact = request.EmergencyContact?.Trim(),
            Notes = request.Notes?.Trim(), CreatedBy = actorId
        };
        tenant.Id = Guid.NewGuid().ToString("N");
        value.AssignTenant(tenant.Id);
        value.ClearBookingAnnouncement();
        value.UpdatedBy = actorId;
        var statusHistory = StatusHistory(value, previous, "Tenant assigned", actorId);
        var audit = Audit("tenant.assigned", value.Id, actorId, new() { ["tenantId"] = tenant.Id });
        await lifecycle.AssignTenantAsync(value, tenant, statusHistory, audit, null, cancellationToken);
        await bookings.CloseActiveAsync(value.Id, BookingStatus.Converted, actorId, cancellationToken);
        return value.ToDto((await GetBlockAsync(value.BlockId, cancellationToken)).BlockName);
    }

    public async Task<PropertyDto> UpdateTenantAsync(
        string id, AssignTenantRequest request, string actorId, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(value.CurrentTenantId))
            throw new DomainRuleException("This property does not have an active tenant.", "ACTIVE_TENANT_NOT_FOUND");

        var tenant = await tenants.GetByIdAsync(value.CurrentTenantId, cancellationToken)
            ?? throw new DomainRuleException("The active tenant record could not be found.", "TENANT_NOT_FOUND");
        if (tenant.Status != TenantStatus.Active || tenant.PropertyId != value.Id)
            throw new DomainRuleException("The tenant is not active for this property.", "TENANT_NOT_ACTIVE");

        tenant.Cid = request.Cid;
        tenant.FullName = request.FullName.Trim();
        tenant.PhoneNumber = request.PhoneNumber.Trim();
        tenant.DiscordId = request.DiscordId.Trim();
        tenant.StartDate = request.StartDate;
        tenant.ExpectedEndDate = request.ExpectedEndDate;
        tenant.MonthlyRent = request.MonthlyRent!.Value;
        tenant.SecurityDeposit = request.SecurityDeposit ?? 0;
        tenant.EmergencyContact = request.EmergencyContact?.Trim();
        tenant.Notes = request.Notes?.Trim();
        tenant.UpdatedBy = actorId;
        tenant.UpdatedAt = DateTime.UtcNow;

        value.Rent = tenant.MonthlyRent;
        value.SecurityDeposit = tenant.SecurityDeposit;
        value.UpdatedBy = actorId;
        value.UpdatedAt = DateTime.UtcNow;
        var audit = Audit("tenant.updated", value.Id, actorId, new()
        {
            ["tenantId"] = tenant.Id,
            ["cid"] = tenant.Cid
        });
        await lifecycle.UpdateTenantAsync(value, tenant, audit, cancellationToken);
        return await ToManagementDtoAsync(
            value, (await GetBlockAsync(value.BlockId, cancellationToken)).BlockName, cancellationToken);
    }

    public async Task<IReadOnlyList<PropertyBookingDto>> GetBookingsAsync(string propertyId, CancellationToken ct)
    {
        _ = await GetEntityAsync(propertyId, ct);
        var values = await bookings.GetActiveByPropertyAsync(propertyId, ct);
        var creatorIds = values
            .Select(x => x.CreatedBy)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var creatorNames = (await users.GetByIdsAsync(creatorIds, ct))
            .ToDictionary(x => x.Id, x => x.DisplayName, StringComparer.Ordinal);
        return values.Select(booking => ToBookingDto(
            booking,
            !string.IsNullOrWhiteSpace(booking.CreatedBy) && creatorNames.TryGetValue(booking.CreatedBy, out var name)
                ? name
                : null)).ToList();
    }

    public async Task<IReadOnlyList<PropertyBookingGroupDto>> GetAllBookingGroupsAsync(CancellationToken ct)
    {
        var bookingValues = await bookings.GetAllActiveAsync(ct);
        if (bookingValues.Count == 0) return [];

        var propertyValues = await properties.GetByIdsAsync(
            bookingValues.Select(x => x.PropertyId).Distinct(StringComparer.Ordinal).ToArray(), ct);
        var blockNames = await BlockNamesAsync(propertyValues, ct);
        var creatorIds = bookingValues
            .Select(x => x.CreatedBy)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var creatorNames = (await users.GetByIdsAsync(creatorIds, ct))
            .ToDictionary(x => x.Id, x => x.DisplayName, StringComparer.Ordinal);
        var bookingsByProperty = bookingValues
            .GroupBy(x => x.PropertyId, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.ToList(), StringComparer.Ordinal);

        return propertyValues
            .Where(property => bookingsByProperty.ContainsKey(property.Id))
            .OrderByDescending(property => property.BookingAnnouncementPending)
            .ThenBy(property => property.PropertyId)
            .Select(property => new PropertyBookingGroupDto(
                property.Id,
                property.PropertyId,
                property.PropertyName,
                blockNames[property.BlockId],
                property.Type,
                property.Status,
                property.AllowOccupiedBookings,
                property.BookingAnnouncementPending,
                property.BookingAnnouncementCreatedAt,
                property.BookingAnnouncementPostedAt,
                property.BookingAnnouncementPostedByDisplayName,
                bookingsByProperty[property.Id].Select(booking => ToBookingDto(
                    booking,
                    !string.IsNullOrWhiteSpace(booking.CreatedBy) && creatorNames.TryGetValue(booking.CreatedBy, out var name)
                        ? name
                        : null)).ToList()))
            .ToList();
    }

    public async Task<PropertyBookingDto> CreateBookingAsync(
        string propertyId, CreatePropertyBookingRequest request, string actorId, CancellationToken ct)
    {
        var property = await GetEntityAsync(propertyId, ct);
        var activeBookingCount = await bookings.CountActiveByPropertyAsync(property.Id, ct);
        var previous = property.Status;
        property.MarkBooked(null);
        if (activeBookingCount == 0) property.StartBookingAnnouncement();
        property.UpdatedBy = actorId;
        var booking = new PropertyBooking
        {
            PropertyId = property.Id,
            Cid = request.Cid,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            DiscordId = request.DiscordId.Trim(),
            MonthlyRent = request.MonthlyRent!.Value,
            BookingAmount = request.BookingAmount!.Value,
            Notes = request.Notes?.Trim(),
            CreatedBy = actorId,
            UpdatedBy = actorId
        };
        await bookings.CreateAsync(booking, ct);
        await properties.UpdateAsync(property, ct);
        if (previous != property.Status)
            await RecordStatusAsync(property, previous, "Property booking added", actorId, "property.booked", ct);
        await audits.CreateAsync(Audit("property.booking.created", property.Id, actorId,
            new() { ["bookingId"] = booking.Id, ["cid"] = booking.Cid }), ct);
        var creator = await users.GetByIdAsync(actorId, ct);
        return ToBookingDto(booking, creator?.DisplayName);
    }

    public async Task<BookingAnnouncementSummaryDto> GetBookingAnnouncementSummaryAsync(CancellationToken ct) =>
        new(await properties.CountPendingBookingAnnouncementsAsync(ct));

    public async Task<BookingAnnouncementStateDto> SetBookingAnnouncementAsync(
        string propertyId, bool isPosted, string actorId, CancellationToken ct)
    {
        var property = await GetEntityAsync(propertyId, ct);
        if (await bookings.CountActiveByPropertyAsync(property.Id, ct) == 0)
            throw new DomainRuleException(
                "A booking announcement requires at least one active booking.",
                "ACTIVE_BOOKING_NOT_FOUND");
        var actor = await users.GetByIdAsync(actorId, ct) ?? throw new UnauthorizedAccessException();
        property.SetBookingAnnouncementPosted(isPosted, actorId, actor.DisplayName);
        property.UpdatedBy = actorId;
        await properties.UpdateAsync(property, ct);
        await audits.CreateAsync(Audit(
            isPosted ? "property.booking-announcement.posted" : "property.booking-announcement.reopened",
            property.Id,
            actorId), ct);
        return BookingAnnouncementState(property);
    }

    public async Task CancelBookingAsync(string propertyId, string bookingId, string actorId, CancellationToken ct)
    {
        var property = await GetEntityAsync(propertyId, ct);
        var booking = await bookings.GetByIdAsync(bookingId, ct)
            ?? throw new KeyNotFoundException("Booking not found.");
        if (booking.PropertyId != property.Id || booking.Status != BookingStatus.Active)
            throw new DomainRuleException("This booking is not active for the selected property.", "BOOKING_NOT_ACTIVE");
        booking.Status = BookingStatus.Cancelled;
        booking.ClosedAt = DateTime.UtcNow;
        booking.ClosedByUserId = actorId;
        booking.UpdatedAt = DateTime.UtcNow;
        booking.UpdatedBy = actorId;
        await bookings.UpdateAsync(booking, ct);
        if (await bookings.CountActiveByPropertyAsync(property.Id, ct) == 0)
        {
            PropertyStatus? previous = null;
            if (property.Status == PropertyStatus.Booked)
            {
                previous = property.Status;
                property.MakeAvailable();
            }
            property.ClearBookingAnnouncement();
            property.UpdatedBy = actorId;
            await properties.UpdateAsync(property, ct);
            if (previous.HasValue)
                await RecordStatusAsync(property, previous.Value, "Last booking removed", actorId, "property.booking.released", ct);
        }
        await audits.CreateAsync(Audit("property.booking.cancelled", property.Id, actorId,
            new() { ["bookingId"] = booking.Id }), ct);
    }

    public async Task CloseAllBookingsAsync(string propertyId, string actorId, CancellationToken ct)
    {
        var property = await GetEntityAsync(propertyId, ct);
        var activeBookingCount = await bookings.CountActiveByPropertyAsync(property.Id, ct);
        if (activeBookingCount == 0)
            throw new DomainRuleException("This property does not have any active bookings.", "ACTIVE_BOOKINGS_NOT_FOUND");

        await bookings.CloseActiveAsync(property.Id, BookingStatus.Cancelled, actorId, ct);
        var previous = property.Status;
        if (property.Status == PropertyStatus.Booked && string.IsNullOrWhiteSpace(property.CurrentTenantId))
            property.MakeAvailable();
        property.ClearBookingAnnouncement();
        property.UpdatedBy = actorId;
        await properties.UpdateAsync(property, ct);

        if (previous != property.Status)
            await RecordStatusAsync(property, previous, "All bookings closed", actorId, "property.booking.released", ct);
        await audits.CreateAsync(Audit("property.bookings.closed-all", property.Id, actorId,
            new() { ["bookingCount"] = activeBookingCount }), ct);
    }

    public async Task<PropertyDto> EvictAsync(string id, EvictTenantRequest request, string actorId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
            throw new DomainRuleException("An eviction reason is required.", "REASON_REQUIRED");
        var storageImages = request.StorageImageUrls?.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct().ToList() ?? [];
        if (storageImages.Any(url => !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")))
            throw new DomainRuleException("Every storage image must be a valid HTTP or HTTPS URL.", "INVALID_STORAGE_IMAGE_URL");
        var actor = await users.GetByIdAsync(actorId, cancellationToken) ?? throw new UnauthorizedAccessException();
        if (!actor.Role.CanEvict())
            throw new DomainRuleException("Eviction requires Senior Agent access or higher.", "SENIOR_AGENT_REQUIRED");
        var value = await GetEntityAsync(id, cancellationToken);
        var previous = value.Status;
        var tenantId = value.EvictTenant();
        var tenant = await tenants.GetByIdAsync(tenantId, cancellationToken) ?? throw new DomainRuleException("Active tenant record was not found.", "TENANT_NOT_FOUND");
        tenant.Status = TenantStatus.Evicted;
        tenant.EndDate = DateTime.UtcNow;
        tenant.EndReason = request.Reason.Trim();
        tenant.EvictionStorageImages = storageImages;
        tenant.EvictedByUserId = actorId;
        tenant.EvictedByDisplayName = actor.DisplayName;
        tenant.EvictedPropertyName = value.PropertyName;
        tenant.EvictedPropertyId = value.PropertyId;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = actorId;
        value.UpdatedBy = actorId;
        var statusHistory = StatusHistory(value, previous, request.Reason ?? "Tenant evicted", actorId);
        var audit = Audit("tenant.evicted", value.Id, actorId, new() { ["tenantId"] = tenant.Id, ["reason"] = request.Reason });
        await lifecycle.EvictAsync(value, tenant, statusHistory, audit, cancellationToken);
        return value.ToDto((await GetBlockAsync(value.BlockId, cancellationToken)).BlockName);
    }

    public async Task<IReadOnlyList<PropertyStatusHistoryDto>> GetHistoryAsync(string id, CancellationToken cancellationToken)
    {
        _ = await GetEntityAsync(id, cancellationToken);
        return (await history.GetByPropertyAsync(id, cancellationToken)).Select(x => x.ToDto()).ToList();
    }

    public async Task DeleteAsync(string id, string actorId, CancellationToken cancellationToken)
    {
        var value = await GetEntityAsync(id, cancellationToken);
        if (value.CurrentTenantId is not null)
            throw new DomainRuleException("End the active tenancy before deleting a property.", "ACTIVE_TENANCY_EXISTS");
        value.IsDeleted = true;
        value.IsActive = false;
        value.ClearBookingAnnouncement();
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = actorId;
        await bookings.CloseActiveAsync(value.Id, BookingStatus.Cancelled, actorId, cancellationToken);
        await properties.UpdateAsync(value, cancellationToken);
        await audits.CreateAsync(Audit("property.deleted", value.Id, actorId), cancellationToken);
    }

    private async Task RecordStatusAsync(Property value, PropertyStatus previous, string? reason, string actorId, string action, CancellationToken ct)
    {
        await history.CreateAsync(StatusHistory(value, previous, reason, actorId), ct);
        await audits.CreateAsync(Audit(action, value.Id, actorId, new() { ["from"] = previous.ToString(), ["to"] = value.Status.ToString(), ["reason"] = reason }), ct);
    }

    private static PropertyStatusHistory StatusHistory(Property value, PropertyStatus previous, string? reason, string actorId) =>
        new() { PropertyId = value.Id, PreviousStatus = previous, NewStatus = value.Status, Reason = reason, ChangedByUserId = actorId, CreatedBy = actorId };

    private static AuditLog Audit(string action, string id, string actorId, Dictionary<string, object?>? metadata = null) =>
        new() { Action = action, EntityType = "property", EntityId = id, PerformedByUserId = actorId, Metadata = metadata };

    private static Property Map(UpsertPropertyRequest request, Property value)
    {
        value.PropertyId = request.PropertyId; value.BlockId = request.BlockId; value.PropertyName = request.PropertyName.Trim();
        value.Description = request.Description?.Trim(); value.Type = request.Type; value.Storage = request.Storage?.Trim();
        value.Rent = request.Rent; value.SecurityDeposit = request.SecurityDeposit; value.Bedrooms = request.Bedrooms;
        value.Bathrooms = request.Bathrooms; value.Floor = request.Floor; value.Area = request.Area;
        value.FurnishingStatus = request.FurnishingStatus?.Trim(); value.Amenities = request.Amenities?.Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
        value.Images = request.Images?.Distinct().ToList() ?? []; value.IsFeatured = request.IsFeatured; value.IsActive = request.IsActive;
        value.AllowOccupiedBookings = request.AllowOccupiedBookings;
        return value;
    }

    private async Task<Property> GetEntityAsync(string id, CancellationToken ct) => await properties.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("Property not found.");
    private async Task<Block> GetBlockAsync(string id, CancellationToken ct) => await blocks.GetByIdAsync(id, ct) ?? throw new DomainRuleException("Block does not exist.", "BLOCK_NOT_FOUND");
    private async Task<Dictionary<string, string>> BlockNamesAsync(IEnumerable<Property> values, CancellationToken ct)
    {
        var ids = values.Select(x => x.BlockId).Distinct().ToArray();
        var result = (await blocks.GetByIdsAsync(ids, ct)).ToDictionary(x => x.Id, x => x.BlockName);
        if (ids.Any(id => !result.ContainsKey(id)))
            throw new DomainRuleException("Block does not exist.", "BLOCK_NOT_FOUND");
        return result;
    }
    private async Task<PropertyDto> ToManagementDtoAsync(Property value, string blockName, CancellationToken ct)
    {
        var tenant = string.IsNullOrWhiteSpace(value.CurrentTenantId) ? null : await tenants.GetByIdAsync(value.CurrentTenantId, ct);
        var bookingCount = await bookings.CountActiveByPropertyAsync(value.Id, ct);
        return value.ToDto(blockName, tenant, checked((int)bookingCount));
    }

    private static PropertyBookingDto ToBookingDto(PropertyBooking value, string? creatorName) => new(
        value.Id, value.PropertyId, value.Cid, value.FullName, value.PhoneNumber,
        value.DiscordId, value.MonthlyRent, value.BookingAmount, value.Notes,
        value.Status, value.CreatedBy, creatorName, value.CreatedAt);

    private static BookingAnnouncementStateDto BookingAnnouncementState(Property value) => new(
        value.Id,
        value.BookingAnnouncementPending,
        value.BookingAnnouncementCreatedAt,
        value.BookingAnnouncementPostedAt,
        value.BookingAnnouncementPostedByDisplayName);
}
