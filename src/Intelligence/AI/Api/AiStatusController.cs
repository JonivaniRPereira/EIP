using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Intelligence.AI.Application;
using EIP.Shared.Contracts.Analytics;
using Microsoft.AspNetCore.Mvc;

namespace EIP.Intelligence.AI.Api;

[ApiController]
[Route("api/v1/ai/status")]
[RequirePermission(AnalyticsPermissions.AnalyticsQuery)]
public sealed class AiStatusController(IAiModelAdapter adapter, IAiSettingsStore settings) : ControllerBase
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
    public async Task<ActionResult<AiProviderCatalog>> Providers(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(EipClaimTypes.TenantId)?.Value, out var tenantId) || tenantId == Guid.Empty)
        {
            return Forbid();
        }

        var current = await settings.GetAsync(tenantId, cancellationToken);
        return Ok(new AiProviderCatalog(current.ActiveProfileId, false, current.Profiles));
    }
}
