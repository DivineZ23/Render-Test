using ImperialEstates.Api.Extensions;
using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Services;
using ImperialEstates.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperialEstates.Api.Controllers;

[ApiController]
[Authorize(Policy = "Manager")]
[Route("api/v1/users")]
public sealed class UsersController(UserManagementService service) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<UserDto>> All([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] ApprovalStatus? approval = null, [FromQuery] AccessStatus? access = null, [FromQuery] UserRole? role = null, CancellationToken ct = default) => service.QueryAsync(page, pageSize, approval, access, role, ct);
    [HttpGet("pending")]
    public Task<PagedResult<UserDto>> Pending([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) => service.QueryAsync(page, pageSize, ApprovalStatus.Pending, null, null, ct);
    [HttpPost("{id}/approve")] public Task<UserDto> Approve(string id, CancellationToken ct) => service.ApproveAsync(id, User.UserId(), ct);
    [HttpPut("{id}/role")] public Task<UserDto> ChangeRole(string id, ChangeUserRoleRequest request, CancellationToken ct) => service.ChangeRoleAsync(id, request, User.UserId(), ct);
    [HttpPost("{id}/reject")] public Task<UserDto> Reject(string id, UserActionRequest request, CancellationToken ct) => service.RejectAsync(id, User.UserId(), request.Reason, ct);
    [HttpPost("{id}/promote")] public Task<UserDto> Promote(string id, CancellationToken ct) => service.PromoteAsync(id, User.UserId(), ct);
    [HttpPost("{id}/demote")] public Task<UserDto> Demote(string id, UserActionRequest request, CancellationToken ct) => service.DemoteAsync(id, User.UserId(), request.Reason, ct);
    [HttpPost("{id}/revoke")] public Task<UserDto> Revoke(string id, UserActionRequest request, CancellationToken ct) => service.RevokeAsync(id, User.UserId(), request.Reason, ct);
    [HttpPost("{id}/restore")] public Task<UserDto> Restore(string id, CancellationToken ct) => service.RestoreAsync(id, User.UserId(), ct);
    [HttpDelete("{id}")] public async Task<IActionResult> Delete(string id, [FromBody] UserActionRequest request, CancellationToken ct) { await service.DeleteAsync(id, User.UserId(), request.Reason, ct); return NoContent(); }
}
