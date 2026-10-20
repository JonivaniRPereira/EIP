using EIP.Intelligence.AI.Application;
using Microsoft.AspNetCore.DataProtection;

namespace EIP.Intelligence.AI.Infrastructure;

public sealed class DataProtectionAiCredentialProtector(IDataProtectionProvider provider) : IAiCredentialProtector
{
    public bool Available => true;

    public string Protect(Guid tenantId, string profileId, string providerName, string secret) =>
        Scoped(tenantId, profileId, providerName).Protect(secret);

    public string Unprotect(Guid tenantId, string profileId, string providerName, string protectedValue) =>
        Scoped(tenantId, profileId, providerName).Unprotect(protectedValue);

    private IDataProtector Scoped(Guid tenantId, string profileId, string providerName) => provider
        .CreateProtector("EIP.AI.ProviderCredential.v1")
        .CreateProtector(tenantId.ToString("N"))
        .CreateProtector(profileId)
        .CreateProtector(providerName);
}

public sealed class UnavailableAiCredentialProtector : IAiCredentialProtector
{
    public bool Available => false;
    public string Protect(Guid tenantId, string profileId, string provider, string secret) => throw Unavailable();
    public string Unprotect(Guid tenantId, string profileId, string provider, string protectedValue) => throw Unavailable();
    private static InvalidOperationException Unavailable() => new("Proteção de credenciais indisponível neste ambiente.");
}
