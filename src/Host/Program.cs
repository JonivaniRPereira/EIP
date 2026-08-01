using System.Text;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Identity.Application;
using EIP.Platform.Identity.Application.Abstractions;
using EIP.Platform.Identity.Domain;
using EIP.Platform.Identity.Infrastructure;
using EIP.Platform.Tenant.Infrastructure;
using EIP.Shared.Contracts.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Contexto de tenant (AsyncLocal — singleton correto: seu estado é por fluxo assíncrono, não por
// escopo de DI; ver EIP.BuildingBlocks.Security).
builder.Services.AddSingleton<ITenantContextAccessor, AsyncLocalTenantContextAccessor>();
builder.Services.AddSingleton<TenantSessionContextInterceptor>();

var tenantConnectionString = builder.Configuration.GetConnectionString("TenantDb")
    ?? throw new InvalidOperationException("ConnectionStrings:TenantDb não configurado.");
var identityConnectionString = builder.Configuration.GetConnectionString("IdentityDb")
    ?? throw new InvalidOperationException("ConnectionStrings:IdentityDb não configurado.");

// IDbContextFactory (não DbContext escopado): garante que MembershipDirectory sempre abra uma
// conexão nova ao usar a sentinela de sistema (ver EIP.Platform.Tenant.Infrastructure.MembershipDirectory).
builder.Services.AddDbContextFactory<TenantDbContext>((sp, options) =>
    options.UseSqlServer(tenantConnectionString)
        .AddInterceptors(sp.GetRequiredService<TenantSessionContextInterceptor>()));

builder.Services.AddDbContext<AppIdentityDbContext>(options => options.UseSqlServer(identityConnectionString));

builder.Services.AddScoped<IMembershipDirectory, EIP.Platform.Tenant.Infrastructure.MembershipDirectory>();
builder.Services.AddScoped<IRefreshTokenStore, RefreshTokenStore>();
builder.Services.AddScoped<IAuthService, AuthService>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException($"Seção '{JwtOptions.SectionName}' não configurada.");
builder.Services.AddSingleton(Options.Create(jwtOptions));
builder.Services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppIdentityDbContext>();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Mantém os nomes de claim exatamente como emitidos (ex. "sub") em vez do remapeamento
        // legado para URIs longas de ClaimTypes.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

// Popula o ITenantContextAccessor a partir do claim tenant_id do JWT já validado — nunca de input
// não autenticado (docs/08-Multi-Tenant.md §5.1).
app.Use(async (context, next) =>
{
    var tenantClaim = context.User.FindFirst(JwtTokenGenerator.TenantIdClaimType)?.Value;
    if (Guid.TryParse(tenantClaim, out var tenantId))
    {
        var tenantContextAccessor = context.RequestServices.GetRequiredService<ITenantContextAccessor>();
        tenantContextAccessor.Current = new TenantContext(tenantId);
    }

    await next();
});

app.MapControllers();

app.Run();
