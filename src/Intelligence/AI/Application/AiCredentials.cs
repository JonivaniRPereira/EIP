using System.Text.Json.Serialization;

namespace EIP.Intelligence.AI.Application;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class SaveAiCredentialRequest
{
    public long Revision { get; init; }
    public string Secret { get; init; } = string.Empty;
    public override string ToString() => "SaveAiCredentialRequest [REDACTED]";
}

public sealed record AiCredentialStatus(long Revision, bool ProtectionAvailable, IReadOnlyList<string> ConfiguredProfileIds);
public sealed record AiCredentialMutationResult(string Code, AiCredentialStatus? Status);

public interface IAiCredentialProtector
{
    bool Available { get; }
    string Protect(Guid tenantId, string profileId, string provider, string secret);
    string Unprotect(Guid tenantId, string profileId, string provider, string protectedValue);
}

public interface IAiCredentialStore
{
    Task<AiCredentialStatus> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<AiCredentialMutationResult> SaveAsync(Guid tenantId, Guid actorId, string profileId, SaveAiCredentialRequest request, CancellationToken cancellationToken);
    Task<AiCredentialMutationResult> RemoveAsync(Guid tenantId, Guid actorId, string profileId, long revision, CancellationToken cancellationToken);
}
