using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Intelligence.AI.Application;
using EIP.Shared.Contracts.AI;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EIP.Intelligence.AI.Api;

[ApiController]
[Route("api/v1/ai/settings")]
[RequirePermission(AiPermissions.SettingsManage)]
public sealed partial class AiSettingsController(IAiSettingsStore store, AiProviderConfiguration configuration,
    ILogger<AiSettingsController> logger) : ControllerBase
{
    [HttpGet("providers")]
    public ActionResult<IReadOnlyList<string>> Providers()
    {
        if (!Guid.TryParse(User.FindFirst(EipClaimTypes.TenantId)?.Value, out var tenantId) || tenantId == Guid.Empty)
        {
            return Forbid();
        }

        return Ok(configuration.Catalog.Profiles.Select(profile => profile.Provider).Distinct(StringComparer.Ordinal).ToArray());
    }

    [HttpGet]
    public async Task<ActionResult<AiTenantSettings>> Get(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(EipClaimTypes.TenantId)?.Value, out var tenantId) || tenantId == Guid.Empty)
        {
            return Forbid();
        }

        return Ok(await store.GetAsync(tenantId, cancellationToken));
    }

    [HttpPut]
    [RequestSizeLimit(32768)]
    public async Task<ActionResult<AiTenantSettings>> Save(SaveAiSettingsRequest request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirst(EipClaimTypes.TenantId)?.Value, out var tenantId) || tenantId == Guid.Empty
            || !Guid.TryParse(User.FindFirst("sub")?.Value, out var actorId) || actorId == Guid.Empty)
        {
            return Forbid();
        }

        AiTenantSettings? saved;
        try
        {
            saved = await store.SaveAsync(tenantId, actorId, request, cancellationToken);
        }
        catch (ArgumentException)
        {
            return Problem(detail: "Configuração inválida. Confira provedor, identificadores únicos, modelo selecionado e limites.", statusCode: 400);
        }

        if (saved is null)
        {
            return Problem(detail: "A configuração mudou em outra sessão. Recarregue antes de salvar.", statusCode: 409);
        }

        LogSaved(tenantId, actorId, saved.Revision);
        return Ok(saved);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "AI settings saved for tenant {TenantId} by {ActorId}; revision {Revision}")]
    private partial void LogSaved(Guid tenantId, Guid actorId, long revision);
}
