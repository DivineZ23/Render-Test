using ImperialEstates.Application.Common;
using ImperialEstates.Application.DTOs;
using ImperialEstates.Application.Interfaces;
using ImperialEstates.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImperialEstates.Api.Controllers;

[ApiController, Authorize(Policy = "Manager"), Route("api/v1/audit-logs")]
public sealed class AuditLogsController(AuditLogService auditLogs) : ControllerBase
{
    [HttpGet]
    public Task<PagedResult<AuditLogDto>> All([FromQuery] AuditLogQuery query, CancellationToken ct) =>
        auditLogs.QueryAsync(query, ct);
}

