using EIP.Data.Connector.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace EIP.Data.Connector.Infrastructure;

public sealed class ReferenceRestOptions
{
    public string[] AllowedEndpoints { get; set; } = [];
    public int MaxResponseBytes { get; set; } = 8 * 1024 * 1024;
}

/// <summary>Lista de URLs exatas mantida pelo operador, nunca pelo tenant. Sem lista, nega tudo.</summary>
public sealed class ReferenceEndpointPolicy(IOptions<ReferenceRestOptions> options) : IReferenceEndpointPolicy
{
    public bool IsAllowed(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) || !string.IsNullOrEmpty(uri.Query))
        {
            return false;
        }

        return options.Value.AllowedEndpoints.Any(allowed => Uri.TryCreate(allowed, UriKind.Absolute, out var permitted)
            && string.Equals(uri.AbsoluteUri, permitted.AbsoluteUri, StringComparison.Ordinal));
    }
}
