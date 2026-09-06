using FluentValidation;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Application.Validators;

public sealed class UpsertBlockRequestValidator : AbstractValidator<UpsertBlockRequest>
{
    public UpsertBlockRequestValidator()
    {
        RuleFor(x => x.BlockId).GreaterThan(0);
        RuleFor(x => x.BlockName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.Address).MaximumLength(500);
    }
}

public sealed class UpsertPropertyRequestValidator : AbstractValidator<UpsertPropertyRequest>
{
    public UpsertPropertyRequestValidator()
    {
        RuleFor(x => x.PropertyId).GreaterThan(0);
        RuleFor(x => x.BlockId).NotEmpty();
        RuleFor(x => x.PropertyName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Type)
            .Must(type => type.IsSupportedInterior())
            .WithMessage("Select a supported interior structure.");
        RuleFor(x => x.Rent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SecurityDeposit).GreaterThanOrEqualTo(0).When(x => x.SecurityDeposit.HasValue);
        RuleFor(x => x.Bedrooms).GreaterThanOrEqualTo(0).When(x => x.Bedrooms.HasValue);
        RuleForEach(x => x.Images).Must(uri => Uri.TryCreate(uri, UriKind.RelativeOrAbsolute, out _)).WithMessage("Image URL is invalid.");
    }
}

public sealed class AssignTenantRequestValidator : AbstractValidator<AssignTenantRequest>
{
    public AssignTenantRequestValidator()
    {
        RuleFor(x => x.Cid).GreaterThan(0);
        RuleFor(x => x.DiscordId).NotEmpty().Matches(@"^\d+$").MaximumLength(32);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\d{3}-\d{4}$")
            .WithMessage("Phone number must use the format 123-4567.");
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.MonthlyRent).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.SecurityDeposit).GreaterThanOrEqualTo(0).When(x => x.SecurityDeposit.HasValue);
    }
}

public sealed class CreatePropertyBookingRequestValidator : AbstractValidator<CreatePropertyBookingRequest>
{
    public CreatePropertyBookingRequestValidator()
    {
        RuleFor(x => x.Cid).GreaterThan(0);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\d{3}-\d{4}$")
            .WithMessage("Phone number must use the format 123-4567.");
        RuleFor(x => x.DiscordId).NotEmpty().Matches(@"^\d+$").MaximumLength(32);
        RuleFor(x => x.MonthlyRent).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.BookingAmount).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class PreviewAuctionCommissionRequestValidator : AbstractValidator<PreviewAuctionCommissionRequest>
{
    public PreviewAuctionCommissionRequestValidator()
    {
        RuleFor(x => x.BasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FinalAuctionPrice).GreaterThanOrEqualTo(x => x.BasePrice);
        RuleFor(x => x.TotalNumberOfAgents).GreaterThanOrEqualTo(1);
    }
}

public sealed class CreateAuctionSettlementRequestValidator : AbstractValidator<CreateAuctionSettlementRequest>
{
    public CreateAuctionSettlementRequestValidator()
    {
        RuleFor(x => x.AuctionReference).NotEmpty().MaximumLength(160);
        RuleFor(x => x.BasePrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.FinalAuctionPrice).GreaterThanOrEqualTo(x => x.BasePrice);
        RuleFor(x => x.WinningAgentUserId).NotEmpty();
        RuleFor(x => x.OtherAgentUserIds).NotNull();
    }
}

public sealed class CreateEnquiryRequestValidator : AbstractValidator<CreateEnquiryRequest>
{
    public CreateEnquiryRequestValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\d{3}-\d{4}$")
            .WithMessage("Phone number must use the format 123-4567.");
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Message).MaximumLength(2000);
    }
}

public sealed class UpdateUserProfileRequestValidator : AbstractValidator<UpdateUserProfileRequest>
{
    public UpdateUserProfileRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Cid).GreaterThan(0);
        RuleFor(x => x.PhoneNumber)
            .NotEmpty()
            .Matches(@"^\d{3}-\d{4}$")
            .WithMessage("Phone number must use the format 123-4567.");
    }
}

public sealed class CreateRecruitmentApplicationRequestValidator : AbstractValidator<CreateRecruitmentApplicationRequest>
{
    public CreateRecruitmentApplicationRequestValidator()
    {
        RuleFor(x => x.CharacterName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.CharacterCid).GreaterThan(0);
        RuleFor(x => x.CharacterPhoneNumber)
            .NotEmpty()
            .Matches(@"^\d{3}-\d{4}$")
            .WithMessage("Character phone number must use the format 123-4567.");
        RuleFor(x => x.DiscordId)
            .NotEmpty()
            .Matches(@"^\d{15,22}$")
            .WithMessage("Discord ID must be a valid numeric account ID.");
        RuleFor(x => x.ReasonToJoin).NotEmpty().MinimumLength(20).MaximumLength(2000);
        RuleFor(x => x.TotalPlaytime).NotEmpty().MaximumLength(120);
        RuleFor(x => x.BeneficialSkills).NotEmpty().MinimumLength(20).MaximumLength(2000);
        RuleFor(x => x.Availability).NotEmpty().MaximumLength(1000);
    }
}

public sealed class ReviewRecruitmentApplicationRequestValidator : AbstractValidator<ReviewRecruitmentApplicationRequest>
{
    public ReviewRecruitmentApplicationRequestValidator()
    {
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.ReviewNotes).MaximumLength(2000);
        RuleFor(x => x.ReviewNotes)
            .NotEmpty()
            .When(x => x.Status == RecruitmentStatus.Rejected)
            .WithMessage("Add a reason when rejecting an application.");
    }
}
