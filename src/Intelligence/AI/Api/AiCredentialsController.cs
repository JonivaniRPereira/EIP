using EIP.BuildingBlocks.Security;
using EIP.BuildingBlocks.Security.Authorization;
using EIP.Intelligence.AI.Application;
using EIP.Shared.Contracts.AI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EIP.Intelligence.AI.Api;

[ApiController]
[Route("api/v1/ai/settings")]
[RequirePermission(AiPermissions.SettingsManage)]
public sealed partial class AiCredentialsController(IAiCredentialStore store, ILogger<AiCredentialsController> logger) : ControllerBase
{
    [HttpGet("credentials")]
    public async Task<ActionResult<AiCredentialStatus>> Status(CancellationToken cancellationToken)
    {
        var tenantId = Tenant();
        if (tenantId is null)
        {
            return Forbid();
        }

        return Ok(await store.GetStatusAsync(tenantId.Value, cancellationToken));
    }

    [HttpPut("profiles/{profileId}/credential")]
    [RequestSizeLimit(8192)]
    public async Task<ActionResult<AiCredentialStatus>> Save(string profileId, SaveAiCredentialRequest request, CancellationToken cancellationToken)
    {
        var tenantId = Tenant();
        if (tenantId is null || !Guid.TryParse(User.FindFirst("sub")?.Value, out var actor) || actor == Guid.Empty)
        {
            return Forbid();
        }

        AiCredentialMutationResult result;
        try
        {
            result = await store.SaveAsync(tenantId.Value, actor, profileId, request, cancellationToken);
        }
        catch (ArgumentException)
        {
            return Problem(detail: "Credencial inválida. Use de 8 a 2.048 caracteres, sem espaços ou quebras de linha.", statusCode: 400);
        }

        if (result.Code == "ok")
        {
            LogChanged(tenantId.Value, actor, result.Status!.Revision, "stored");
        }

        return ToResponse(result);
    }

    [HttpDelete("profiles/{profileId}/credential")]
    public async Task<ActionResult<AiCredentialStatus>> Remove(string profileId, [FromQuery] long revision, CancellationToken cancellationToken)
    {
        var tenantId = Tenant();
        if (tenantId is null || !Guid.TryParse(User.FindFirst("sub")?.Value, out var actor) || actor == Guid.Empty)
        {
            return Forbid();
        }

        var result = await store.RemoveAsync(tenantId.Value, actor, profileId, revision, cancellationToken);
        if (result.Code == "ok")
        {
            LogChanged(tenantId.Value, actor, result.Status!.Revision, "removed");
        }

        return ToResponse(result);
    }

    private ActionResult<AiCredentialStatus> ToResponse(AiCredentialMutationResult result) => result.Code switch
    {
        "ok" => Ok(result.Status),
        "conflict" => Problem(detail: "Salve os perfis e recarregue a configuração antes de alterar credenciais.", statusCode: 409),
        "profile_not_found" => Problem(detail: "Perfil não encontrado nesta organização.", statusCode: 404),
        _ => Problem(detail: "Proteção de credenciais indisponível neste ambiente.", statusCode: 503),
    };

    private Guid? Tenant() => Guid.TryParse(User.FindFirst(EipClaimTypes.TenantId)?.Value, out var id) && id != Guid.Empty ? id : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "AI credential {Action} for tenant {TenantId} by {ActorId}; revision {Revision}")]
    private partial void LogChanged(Guid tenantId, Guid actorId, long revision, string action);
}
