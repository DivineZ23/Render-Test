using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Services;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperialEstates.Api.Controllers;

[ApiController, Authorize(Policy = "ApprovedUser"), Route("api/v1/dashboard")]
public sealed class DashboardController(
    DashboardService service,
    AgentAnalyticsService agentAnalytics,
    ITenantRepository tenants,
    IPropertyRepository properties) : ControllerBase
{
    [HttpGet] public Task<DashboardSummaryDto> Get(CancellationToken ct) => service.GetAsync(ct);

    [HttpGet("personal")]
    public async Task<PersonalStatisticsDto> Personal(CancellationToken ct)
    {
        var userId = User.UserId();
        var allTenantsTask = tenants.GetAllAsync(ct);
        var allPropertiesTask = properties.GetAllAsync(ct);
        await Task.WhenAll(allTenantsTask, allPropertiesTask);
        var propertyNames = allPropertiesTask.Result.ToDictionary(x => x.Id, x => x.PropertyName);
        var sales = allTenantsTask.Result.Where(x => x.CreatedBy == userId).OrderByDescending(x => x.CreatedAt).ToList();
        var evictions = allTenantsTask.Result.Where(x => x.EvictedByUserId == userId && x.EndDate.HasValue).OrderByDescending(x => x.EndDate).ToList();
        PersonalActivityDto Map(ImperialEstates.Domain.Entities.Tenant tenant, DateTime occurredAt, decimal amount) => new(
            tenant.Id, tenant.PropertyId,
            tenant.EvictedPropertyName ?? propertyNames.GetValueOrDefault(tenant.PropertyId, "Unknown property"),
            tenant.FullName, tenant.Cid, amount, occurredAt);
        return new(
            sales.Count, evictions.Count, sales.Sum(x => x.SecurityDeposit),
            sales.Take(6).Select(x => Map(x, x.CreatedAt, x.SecurityDeposit)).ToList(),
            evictions.Take(6).Select(x => Map(x, x.EndDate!.Value, x.MonthlyRent)).ToList());
    }

    [Authorize(Policy = "Manager"), HttpGet("agents")]
    public Task<AgentAnalyticsDto> Agents(DateTime? from, DateTime? to, CancellationToken ct) =>
        agentAnalytics.GetAsync(from, to, ct);

    [Authorize(Policy = "Manager"), HttpGet("agents/{userId}")]
    public Task<AgentAnalyticsDetailDto> Agent(
        string userId, DateTime? from, DateTime? to, CancellationToken ct) =>
        agentAnalytics.GetDetailAsync(userId, from, to, ct);
}
