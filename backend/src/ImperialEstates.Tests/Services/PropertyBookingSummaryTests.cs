using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Application.Services;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Tests.Services;

public sealed class PropertyBookingSummaryTests
{
    [Fact]
    public async Task Uses_highest_booking_rent_instead_of_listed_rent()
    {
        var bookings = new BookingRepository(10000m, 25000.50m, 15000m);
        var result = await Service(bookings).GetBookingSummaryAsync("property-1", default);
        Assert.Equal(3, result.BookingCount);
        Assert.Equal(25000.50m, result.HighestRent);
        Assert.Equal("property-1", bookings.RequestedId);
    }

    [Fact]
    public async Task No_bookings_returns_no_rent_to_trigger_prompt()
    {
        var result = await Service(new BookingRepository()).GetBookingSummaryAsync("property-1", default);
        Assert.Equal(0, result.BookingCount);
        Assert.Null(result.HighestRent);
    }

    [Fact]
    public async Task Zero_rent_booking_is_not_treated_as_missing_rent()
    {
        var result = await Service(new BookingRepository(0m)).GetBookingSummaryAsync("property-1", default);
        Assert.Equal(1, result.BookingCount);
        Assert.Equal(0m, result.HighestRent);
    }

    [Fact]
    public async Task Missing_property_does_not_read_bookings()
    {
        var bookings = new BookingRepository();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(bookings).GetBookingSummaryAsync("missing", default));
        Assert.Null(bookings.RequestedId);
    }

    private static PropertyService Service(BookingRepository bookings) =>
        new(new PropertyRepository(), null!, null!, null!, null!, null!, null!, bookings);

    private sealed class BookingRepository(params decimal[] rents) : IPropertyBookingRepository
    {
        public string? RequestedId { get; private set; }
        public Task<IReadOnlyList<PropertyBooking>> GetActiveByPropertyAsync(string propertyId, CancellationToken ct)
        {
            RequestedId = propertyId;
            return Task.FromResult<IReadOnlyList<PropertyBooking>>(rents.Select(rent => new PropertyBooking
                { PropertyId = propertyId, MonthlyRent = rent, Status = BookingStatus.Active }).ToList());
        }
        public Task<IReadOnlyList<PropertyBooking>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PropertyBooking>> GetAllActiveAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<PropertyBooking?> GetByIdAsync(string id, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountActiveByPropertyAsync(string propertyId, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(PropertyBooking booking, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(PropertyBooking booking, CancellationToken ct) => throw new NotSupportedException();
        public Task CloseActiveAsync(string propertyId, BookingStatus status, string actorId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class PropertyRepository : IPropertyRepository
    {
        public Task<Property?> GetByIdAsync(string id, CancellationToken ct) => Task.FromResult<Property?>(
            id == "property-1" ? new Property { Id = id, Rent = 99000m } : null);
        public Task<PagedResult<Property>> QueryAsync(PropertyQuery query, bool publicOnly, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Property>> GetByIdsAsync(IReadOnlyCollection<string> ids, CancellationToken ct) => throw new NotSupportedException();
        public Task<Property?> GetByBusinessIdAsync(int propertyId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Property?> GetByNameAsync(string propertyName, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Property>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<Property>> GetFeaturedAsync(int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountByBlockAsync(string blockId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> CountByStatusAsync(PropertyStatus? status, CancellationToken ct) => throw new NotSupportedException();
        public Task CreateAsync(Property property, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Property property, CancellationToken ct) => throw new NotSupportedException();
    }
}
