using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Entities;
using ImperialEstates.Domain.Enums;
using ImperialEstates.Domain.Exceptions;

namespace ImperialEstates.Application.Services;

public sealed class RecruitmentService(
    IRecruitmentApplicationRepository applications,
    IUserRepository users,
    IAuditRepository audits,
    ISettingRepository settings)
{
    private const string RecruitmentEnabledKey = "recruitment.enabled";

    public async Task<RecruitmentApplicationDto> CreateAsync(
        CreateRecruitmentApplicationRequest request,
        CancellationToken ct)
    {
        if (!(await GetSettingsAsync(ct)).IsEnabled)
            throw new DomainRuleException(
                "Recruitment is currently closed.",
                "RECRUITMENT_CLOSED");

        var discordId = request.DiscordId.Trim();
        if (await applications.HasPendingAsync(request.CharacterCid, discordId, ct))
            throw new DomainRuleException(
                "A pending application already exists for this CID or Discord ID.",
                "RECRUITMENT_APPLICATION_PENDING");

        var value = new RecruitmentApplication
        {
            CharacterName = request.CharacterName.Trim(),
            CharacterCid = request.CharacterCid,
            CharacterPhoneNumber = request.CharacterPhoneNumber.Trim(),
            DiscordId = discordId,
            ReasonToJoin = request.ReasonToJoin.Trim(),
            TotalPlaytime = request.TotalPlaytime.Trim(),
            BeneficialSkills = request.BeneficialSkills.Trim(),
            Availability = request.Availability.Trim()
        };
        await applications.CreateAsync(value, ct);
        return Map(value, null);
    }

    public async Task<PagedResult<RecruitmentApplicationDto>> QueryAsync(
        int page,
        int pageSize,
        RecruitmentStatus? status,
        CancellationToken ct)
    {
        var values = await applications.QueryAsync(page, pageSize, status, ct);
        var reviewerIds = values.Items
            .Select(x => x.ReviewedByUserId)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .Cast<string>()
            .ToArray();
        var reviewers = (await users.GetByIdsAsync(reviewerIds, ct))
            .ToDictionary(value => value.Id, value => value.DisplayName);

        return new(
            values.Items
                .Select(x => Map(x, x.ReviewedByUserId is null ? null : reviewers.GetValueOrDefault(x.ReviewedByUserId)))
                .ToList(),
            values.Page,
            values.PageSize,
            values.TotalItems);
    }

    public async Task<RecruitmentApplicationDto> ReviewAsync(
        string id,
        ReviewRecruitmentApplicationRequest request,
        string actorId,
        CancellationToken ct)
    {
        var value = await applications.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException("Recruitment application not found.");
        var reviewer = await users.GetByIdAsync(actorId, ct)
            ?? throw new UnauthorizedAccessException();

        value.Status = request.Status;
        value.ReviewNotes = request.ReviewNotes?.Trim();
        value.ReviewedByUserId = actorId;
        value.ReviewedAt = DateTime.UtcNow;
        value.UpdatedAt = DateTime.UtcNow;
        value.UpdatedBy = actorId;
        await applications.UpdateAsync(value, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = "recruitment.application.reviewed",
            EntityType = "recruitment_application",
            EntityId = value.Id,
            PerformedByUserId = actorId,
            Metadata = new()
            {
                ["status"] = request.Status.ToString(),
                ["reviewNotes"] = value.ReviewNotes
            }
        }, ct);
        return Map(value, reviewer.DisplayName);
    }

    public async Task<RecruitmentSettingsDto> GetSettingsAsync(CancellationToken ct)
    {
        var value = await settings.GetAsync(RecruitmentEnabledKey, ct);
        return new(value is null || !bool.TryParse(value.Value, out var enabled) || enabled);
    }

    public async Task<RecruitmentSettingsDto> UpdateSettingsAsync(
        UpdateRecruitmentSettingsRequest request,
        string actorId,
        CancellationToken ct)
    {
        var previous = await GetSettingsAsync(ct);
        await settings.UpsertAsync(new ApplicationSetting
        {
            Key = RecruitmentEnabledKey,
            Value = request.IsEnabled.ToString(),
            IsPublic = true,
            UpdatedBy = actorId
        }, ct);
        await audits.CreateAsync(new AuditLog
        {
            Action = "recruitment.settings.updated",
            EntityType = "application_setting",
            EntityId = RecruitmentEnabledKey,
            PerformedByUserId = actorId,
            PreviousValues = new() { ["isEnabled"] = previous.IsEnabled },
            NewValues = new() { ["isEnabled"] = request.IsEnabled }
        }, ct);
        return new(request.IsEnabled);
    }

    private static RecruitmentApplicationDto Map(RecruitmentApplication value, string? reviewerName) => new(
        value.Id,
        value.CharacterName,
        value.CharacterCid,
        value.CharacterPhoneNumber,
        value.DiscordId,
        value.ReasonToJoin,
        value.TotalPlaytime,
        value.BeneficialSkills,
        value.Availability,
        value.Status,
        value.ReviewedByUserId,
        reviewerName,
        value.ReviewedAt,
        value.ReviewNotes,
        value.CreatedAt,
        value.UpdatedAt);
}
