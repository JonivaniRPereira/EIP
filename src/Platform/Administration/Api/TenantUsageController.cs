using EIP.BuildingBlocks.Security;
using EIP.Platform.Administration.Application;
using EIP.Shared.Contracts.Administration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EIP.Platform.Administration.Api;

[ApiController]
[Route("api/v1/admin/tenants/{tenantId:guid}/usage")]
[Authorize(Policy = PlatformPermissions.TenantUsageRead)]
public sealed partial class TenantUsageController : ControllerBase
{
    private readonly ITenantUsageReader _reader;
    private readonly ILogger<TenantUsageController> _logger;

    public TenantUsageController(ITenantUsageReader reader, ILogger<TenantUsageController> logger)
    {
        _reader = reader;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<TenantUsageReport>> Get(Guid tenantId, [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || tenantId == TenantContext.SystemTenantId || from == default || to == default || from >= to || to - from > TimeSpan.FromDays(366))
        {
            return Problem(detail: "Informe um tenant válido e um período from/to de até 366 dias, com from < to.", statusCode: StatusCodes.Status400BadRequest);
        }

        var report = await _reader.ReadAsync(tenantId, from.ToUniversalTime(), to.ToUniversalTime(), cancellationToken);
        if (report is not null)
        {
            LogUsageViewed(tenantId, User.FindFirst("sub")?.Value, report.From, report.To);
        }

        return report is null ? NotFound() : Ok(report);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Tenant usage viewed: tenantId={TenantId} userId={UserId} from={From} to={To}")]
    private partial void LogUsageViewed(Guid tenantId, string? userId, DateTimeOffset from, DateTimeOffset to);
}
