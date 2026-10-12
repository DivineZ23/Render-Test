using System.Security.Claims;
using ImperialEstates.Api.Authorization;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ImperialEstates.Api.Middleware;

// Enforce the live role before authorization, including endpoints with only [Authorize].
public sealed class StateManagerAccessMiddleware(RequestDelegate next)
{
    public static readonly object VerifiedReadAccess = new();

    public async Task InvokeAsync(HttpContext context, IUserRepository users, ISettingRepository settings)
    {
        var endpoint = context.GetEndpoint();
        var action = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();
        var id = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated != true || id is null || action is null)
        {
            await next(context);
            return;
        }
        var user = await users.GetByIdAsync(id, context.RequestAborted);
        if (user?.Role != UserRole.StateManager)
        {
            await next(context);
            return;
        }
        var read = HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method);
        if (action.ControllerName == "Auth" && action.ActionName == "Logout" && HttpMethods.IsPost(context.Request.Method))
        {
            await next(context);
            return;
        }
        if (!read)
        {
            await Deny(context, "READ_ONLY_ROLE", "State Managers have view-only access.");
            return;
        }
        // Public data and login/session endpoints do not grant dashboard access.
        if (endpoint!.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
            action.ControllerName == "Auth" && action.ActionName is "Me" or "Discord" or "Callback")
        {
            await next(context);
            return;
        }
        if (user.IsDeleted || user.ApprovalStatus != ApprovalStatus.Approved || user.AccessStatus != AccessStatus.Active)
        {
            await Deny(context, "ACCESS_DENIED", "Your account must be approved and active.");
            return;
        }
        // Navigation needs its permission map even when the editor page is hidden.
        if (action.ControllerName == "AccessManagement" && action.ActionName == "Get")
        {
            await next(context);
            return;
        }
        var permissions = await AccessPermissions.ReadAsync(settings, context.RequestAborted);
        var resources = ReadResources(action.ControllerName, action.ActionName, context.Request.Query);
        if (!resources.Any(key => AccessPermissions.CanStateManagerView(key) &&
            permissions.TryGetValue(key, out var roles) && roles.GetValueOrDefault("stateManager")))
        {
            await Deny(context, "PAGE_ACCESS_DENIED", "The Owner has not enabled access to this page for State Managers.");
            return;
        }
        context.Items[VerifiedReadAccess] = true;
        await next(context);
    }

    // Explicit allow-list: future private endpoints stay inaccessible until mapped.
    public static string[] ReadResources(string controller, string action, IQueryCollection query) => (controller, action) switch
    {
        ("Dashboard", "Get") => ["overview"],
        ("Dashboard", "Personal") => ["analytics"],
        ("Dashboard", "Agents" or "Agent") => ["agentAnalytics"],
        ("Commissions", "Get") => ["commissions"],
        ("Users", "All" or "Pending") => ["administration.users"],
        ("AuditLogs", "All") => ["administration.auditLogs"],
        ("Blocks", "All" or "Get") => ["portfolio.blocks", "portfolio.properties"],
        ("Properties", "All" or "Details" or "History") => ["portfolio.properties"],
        ("Properties", "AllBookings" or "Bookings" or "BookingAnnouncementSummary") => ["portfolio.bookings"],
        ("Tenants", "All") => ["portfolio.tenants"],
        ("Tenants", "Evictions") => ["notices.evictionHistory"],
        ("Team", "Agents") => ["team", "notices.syncedDataRecords"],
        ("Notices", "Feed" or "FeedSummary") => QueryStatus(query, "evictable") switch
        {
            "evictable" => ["notices.eviction"], "overdue" => ["notices.overdue"], _ => []
        },
        ("Notices", "Records") when QueryStatus(query, "overdue") == "overdue" => ["notices.overdueList"],
        ("Notices", "Snapshot") => ["notices.sync"],
        ("Notices", "Snapshots") when string.Equals(query["summaryOnly"], "true", StringComparison.OrdinalIgnoreCase) => ["notices.syncedDataRecords"],
        ("Notices", "EvictionQueue") => ["notices.evictionQueue"],
        ("Recruitment", "All") => QueryStatus(query, "") switch
        {
            "pending" => ["recruitment.pending"], "accepted" => ["recruitment.accepted"],
            "rejected" => ["recruitment.rejected"], _ => []
        },
        _ => []
    };

    private static string QueryStatus(IQueryCollection query, string fallback) =>
        query.ContainsKey("status") ? query["status"].ToString().ToLowerInvariant() : fallback;

    private static Task Deny(HttpContext context, string code, string message)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return context.Response.WriteAsJsonAsync(new { statusCode = 403, errorCode = code, message, errors = (object?)null, traceId = context.TraceIdentifier });
    }
}
