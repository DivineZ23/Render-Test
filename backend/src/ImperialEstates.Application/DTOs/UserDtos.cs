using ImperialEstates.Domain.Enums;

namespace ImperialEstates.Application.DTOs;

public sealed record UserDto(
    string Id, string DiscordUserId, string Username, string DisplayName, string? AvatarUrl, string? Email,
    string? FullName, int? Cid, string? PhoneNumber,
    UserRole Role, ApprovalStatus ApprovalStatus, AccessStatus AccessStatus, string? ApprovedBy,
    DateTime? ApprovedAt, string? RevokedBy, DateTime? RevokedAt, string? RevokeReason,
    DateTime? LastLoginAt, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record CurrentUserDto(
    string Id, string DisplayName, string Username, string? AvatarUrl, UserRole Role,
    ApprovalStatus ApprovalStatus, AccessStatus AccessStatus);

public sealed record AgentSummaryDto(string Id, string DisplayName, string Username, string? AvatarUrl, UserRole Role);

public sealed record AccessManagementDto(Dictionary<string, Dictionary<string, bool>> Permissions);
public sealed record TeamMemberDto(string Id, string Name, string Title, string Biography, string ImageUrl);

public sealed record UserActionRequest(string? Reason);
public sealed record ChangeUserRoleRequest(UserRole Role, string? Reason);
public sealed record UpdateUserProfileRequest(string FullName, int Cid, string PhoneNumber);
public sealed record DiscordProfile(string Id, string Username, string DisplayName, string? AvatarUrl, string? Email);
public sealed record AuthResult(UserDto User, string AccessToken, DateTime ExpiresAt);
