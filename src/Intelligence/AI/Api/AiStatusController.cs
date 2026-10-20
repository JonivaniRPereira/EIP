using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Intelligence.AI.Application;
using EIP.Shared.Contracts.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace EIP.Intelligence.AI.Api;

[ApiController]
[Route("api/v1/ai/status")]
[RequirePermission(AnalyticsPermissions.AnalyticsQuery)]
public sealed class AiStatusController(IAiModelAdapter adapter, AiProviderConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public ActionResult<AiModelStatus> Get()
    {
        if (!Guid.TryParse(User.FindFirst(EipClaimTypes.TenantId)?.Value, out var tenantId) || tenantId == Guid.Empty)
        {
            return Forbid();
        }

        return Ok(adapter.Status);
    }

    [HttpGet("/api/v1/ai/providers")]
    public ActionResult<AiProviderCatalog> Providers()
    {
        if (!Guid.TryParse(User.FindFirst(EipClaimTypes.TenantId)?.Value, out var tenantId) || tenantId == Guid.Empty)
        {
            return Forbid();
        }

        return Ok(configuration.Catalog);
    }
}
