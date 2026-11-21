using FluentValidation;
using ImperialEstates.Api.Extensions;
using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperialEstates.Api.Controllers;

[ApiController]
[Route("api/v1/properties")]
public sealed class PropertiesController(
    PropertyService service,
    IValidator<UpsertPropertyRequest> propertyValidator,
    IValidator<AssignTenantRequest> tenantValidator,
    IValidator<CreatePropertyBookingRequest> bookingValidator) : ControllerBase
{
    [AllowAnonymous, HttpGet("available")]
    public Task<PagedResult<PublicPropertyDto>> Available([FromQuery] PropertyQuery query, CancellationToken ct) => service.GetAvailableAsync(query, ct);

    [AllowAnonymous, HttpGet("featured")]
    public Task<IReadOnlyList<PublicPropertyDto>> Featured(CancellationToken ct) => service.GetFeaturedAsync(ct);

    [AllowAnonymous, HttpGet("{id}")]
    public Task<PublicPropertyDto> PublicDetails(string id, CancellationToken ct) => service.GetPublicAsync(id, ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet]
    public Task<PagedResult<PropertyDto>> All([FromQuery] PropertyQuery query, CancellationToken ct) => service.GetAllAsync(query, ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("bookings")]
    public Task<IReadOnlyList<PropertyBookingGroupDto>> AllBookings(CancellationToken ct) =>
        service.GetAllBookingGroupsAsync(ct);

    [Authorize(Policy = "Manager"), HttpGet("bookings/announcement-summary")]
    public Task<BookingAnnouncementSummaryDto> BookingAnnouncementSummary(CancellationToken ct) =>
        service.GetBookingAnnouncementSummaryAsync(ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("{id}/manage")]
    public Task<PropertyDto> Details(string id, CancellationToken ct) => service.GetAsync(id, ct);

    [Authorize(Policy = "Manager"), HttpPost]
    public async Task<ActionResult<PropertyDto>> Create(UpsertPropertyRequest request, CancellationToken ct)
    {
        await propertyValidator.ValidateAndThrowAsync(request, ct);
        var value = await service.CreateAsync(request, User.UserId(), ct);
        return CreatedAtAction(nameof(Details), new { id = value.Id }, value);
    }

    [Authorize(Policy = "ApprovedUser"), HttpPut("{id}")]
    public async Task<PropertyDto> Update(string id, UpsertPropertyRequest request, CancellationToken ct)
    {
        await propertyValidator.ValidateAndThrowAsync(request, ct);
        return await service.UpdateAsync(id, request, User.UserId(), ct);
    }

    [Authorize(Policy = "ApprovedUser"), HttpPatch("{id}/status")]
    public Task<PropertyDto> Status(string id, ChangePropertyStatusRequest request, CancellationToken ct) => service.ChangeStatusAsync(id, request, User.UserId(), ct);

    [Authorize(Policy = "ApprovedUser"), HttpPost("{id}/assign-tenant")]
    public async Task<PropertyDto> AssignTenant(string id, AssignTenantRequest request, CancellationToken ct)
    {
        await tenantValidator.ValidateAndThrowAsync(request, ct);
        return await service.AssignTenantAsync(id, request, User.UserId(), ct);
    }

    [Authorize(Policy = "ApprovedUser"), HttpPut("{id}/tenant")]
    public async Task<PropertyDto> UpdateTenant(string id, AssignTenantRequest request, CancellationToken ct)
    {
        await tenantValidator.ValidateAndThrowAsync(request, ct);
        return await service.UpdateTenantAsync(id, request, User.UserId(), ct);
    }

    [Authorize(Policy = "ApprovedUser"), HttpPost("{id}/evict")]
    public Task<PropertyDto> Evict(string id, EvictTenantRequest request, CancellationToken ct) => service.EvictAsync(id, request, User.UserId(), ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("{id}/bookings")]
    public Task<IReadOnlyList<PropertyBookingDto>> Bookings(string id, CancellationToken ct) =>
        service.GetBookingsAsync(id, ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("{id}/booking-summary")]
    public Task<PropertyBookingSummaryDto> BookingSummary(string id, CancellationToken ct) =>
        service.GetBookingSummaryAsync(id, ct);

    [Authorize(Policy = "ApprovedUser"), HttpPost("{id}/bookings")]
    public async Task<ActionResult<PropertyBookingDto>> CreateBooking(
        string id, CreatePropertyBookingRequest request, CancellationToken ct)
    {
        await bookingValidator.ValidateAndThrowAsync(request, ct);
        var value = await service.CreateBookingAsync(id, request, User.UserId(), ct);
        return Created($"/api/v1/properties/{id}/bookings/{value.Id}", value);
    }

    [Authorize(Policy = "ApprovedUser"), HttpDelete("{id}/bookings/{bookingId}")]
    public async Task<IActionResult> CancelBooking(string id, string bookingId, CancellationToken ct)
    {
        await service.CancelBookingAsync(id, bookingId, User.UserId(), ct);
        return NoContent();
    }

    [Authorize(Policy = "Manager"), HttpDelete("{id}/bookings")]
    public async Task<IActionResult> CloseAllBookings(string id, CancellationToken ct)
    {
        await service.CloseAllBookingsAsync(id, User.UserId(), ct);
        return NoContent();
    }

    [Authorize(Policy = "Manager"), HttpPatch("{id}/bookings/announcement")]
    public Task<BookingAnnouncementStateDto> SetBookingAnnouncement(
        string id, SetBookingAnnouncementRequest request, CancellationToken ct) =>
        service.SetBookingAnnouncementAsync(id, request.IsPosted, User.UserId(), ct);

    [Authorize(Policy = "ApprovedUser"), HttpGet("{id}/history")]
    public Task<IReadOnlyList<PropertyStatusHistoryDto>> History(string id, CancellationToken ct) => service.GetHistoryAsync(id, ct);

    [Authorize(Policy = "Manager"), HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct) { await service.DeleteAsync(id, User.UserId(), ct); return NoContent(); }
}
