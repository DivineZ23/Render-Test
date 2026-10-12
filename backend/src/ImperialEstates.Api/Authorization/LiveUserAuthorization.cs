using System.Security.Claims;
using ImperialEstates.Api.Middleware;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace ImperialEstates.Api.Authorization;

public sealed record LiveUserRequirement(bool ManagerOnly) : IAuthorizationRequirement;

public sealed class LiveUserAuthorizationHandler(IUserRepository users) : AuthorizationHandler<LiveUserRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, LiveUserRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return;
        var ct = context.Resource is HttpContext http ? http.RequestAborted : CancellationToken.None;
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null || user.IsDeleted || user.ApprovalStatus != ApprovalStatus.Approved || user.AccessStatus != AccessStatus.Active) return;
        if (user.Role == UserRole.StateManager && context.Resource is HttpContext stateRequest &&
            !(HttpMethods.IsGet(stateRequest.Request.Method) || HttpMethods.IsHead(stateRequest.Request.Method))) return;
        if (requirement.ManagerOnly && user.Role is not (UserRole.Manager or UserRole.Owner))
        {
            if (user.Role != UserRole.StateManager || context.Resource is not HttpContext request ||
                !(HttpMethods.IsGet(request.Request.Method) || HttpMethods.IsHead(request.Request.Method)) ||
                !Equals(request.Items[StateManagerAccessMiddleware.VerifiedReadAccess], true)) return;
        }
        context.Succeed(requirement);
    }
}
