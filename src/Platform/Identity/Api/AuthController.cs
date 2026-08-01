using EIP.Platform.Identity.Api.Contracts;
using EIP.Platform.Identity.Application;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EIP.Platform.Identity.Api;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private const string SubjectClaimType = "sub";

    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Register(RegisterRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request.Email, request.Password, request.DisplayName, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Login(LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request.Email, request.Password, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponseDto>> Refresh(RefreshRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(request.RefreshToken, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("select-tenant")]
    [Authorize]
    public async Task<ActionResult<AuthResponseDto>> SelectTenant(SelectTenantRequestDto request, CancellationToken cancellationToken)
    {
        var subject = User.FindFirst(SubjectClaimType)?.Value;
        if (!Guid.TryParse(subject, out var userId))
        {
            return Problem(detail: "Token não contém identidade válida.", statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await _authService.SelectTenantAsync(userId, request.TenantId, cancellationToken);
        return ToActionResult(result);
    }

    private ActionResult<AuthResponseDto> ToActionResult(AuthResult result)
    {
        if (!result.Succeeded)
        {
            return Problem(detail: result.Error, statusCode: StatusCodes.Status401Unauthorized);
        }

        var dto = new AuthResponseDto(
            result.AccessToken!,
            result.AccessTokenExpiresAtUtc!.Value,
            result.RefreshToken!,
            result.RequiresTenantSelection,
            result.AvailableTenants
                .Select(t => new TenantOptionDto(t.TenantId, t.TenantName, t.TenantSlug))
                .ToList());

        return Ok(dto);
    }
}
