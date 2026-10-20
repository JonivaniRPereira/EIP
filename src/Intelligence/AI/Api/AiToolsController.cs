using System.Text.Json;
using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Intelligence.AI.Application;
using EIP.Shared.Contracts.Analytics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EIP.Intelligence.AI.Api;

[ApiController]
[Route("api/v1/ai/tools")]
[RequirePermission(AnalyticsPermissions.AnalyticsQuery)]
public sealed partial class AiToolsController(ILogger<AiToolsController> logger) : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyList<AiToolDefinition>> List()
    {
        if (SelectedTenant() is null)
        {
            return Forbid();
        }

        return Ok(AiToolRegistry.Tools);
    }

    [HttpPost("{name}")]
    [RequestSizeLimit(AiToolRegistry.MaxInputBytes)]
    public ActionResult<AiDiscoveryResult> Execute(string name, JsonElement arguments)
    {
        var tenantId = SelectedTenant();
        if (tenantId is null)
        {
            return Forbid();
        }

        if (!string.Equals(name, AiToolRegistry.ListDatasets, StringComparison.Ordinal))
        {
            return Problem(detail: "Ferramenta não disponível.", statusCode: StatusCodes.Status404NotFound);
        }

        if (arguments.ValueKind != JsonValueKind.Object || arguments.EnumerateObject().Any())
        {
            return Problem(detail: "Esta ferramenta aceita somente um objeto vazio; o contexto vem da sessão autenticada.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        LogDiscovery(tenantId.Value);
        return Ok(AiToolRegistry.Discover(tenantId.Value));
    }

    private Guid? SelectedTenant() => Guid.TryParse(User.FindFirst(EipClaimTypes.TenantId)?.Value, out var id)
        && id != Guid.Empty ? id : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "AI tool listDatasets invoked for tenant {TenantId}")]
    private partial void LogDiscovery(Guid tenantId);
}
