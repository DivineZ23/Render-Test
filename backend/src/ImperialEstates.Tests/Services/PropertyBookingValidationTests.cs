using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Validators;

namespace ImperialEstates.Tests.Services;

public sealed class PropertyBookingValidationTests
{
    private readonly CreatePropertyBookingRequestValidator _validator = new();

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2999, true)]
    [InlineData(-1, false)]
    public void Booking_amount_accepts_any_non_negative_value(int bookingAmount, bool expectedValid)
    {
        var request = new CreatePropertyBookingRequest(
            245,
            "Alex Mercer",
            "555-0123",
            "727075012489510944",
            3000,
            bookingAmount,
            null);

        var result = _validator.Validate(request);

        Assert.Equal(expectedValid, result.IsValid);
    }
}
