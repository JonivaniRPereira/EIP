using System.Security.Cryptography;
using System.Text.Json;
using EIP.Intelligence.AI.Application;
using Microsoft.EntityFrameworkCore;

namespace EIP.Intelligence.AI.Infrastructure;

internal sealed record ProtectedAiCredential(string Provider, string Value);

internal static class AiCredentialEnvelope
{
    public static Dictionary<string, ProtectedAiCredential> Read(string? json) => json is null ? new(StringComparer.Ordinal)
        : JsonSerializer.Deserialize<Dictionary<string, ProtectedAiCredential>>(json)!;
    public static string? Write(Dictionary<string, ProtectedAiCredential> credentials) => credentials.Count == 0 ? null : JsonSerializer.Serialize(credentials);
}

public sealed class AiCredentialStore(IDbContextFactory<AiDbContext> factory, IAiCredentialProtector protector) : IAiCredentialStore
{
    public async Task<AiCredentialStatus> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.Settings.AsNoTracking().SingleOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken);
        return Status(record);
    }

    public Task<AiCredentialMutationResult> SaveAsync(Guid tenantId, Guid actorId, string profileId, SaveAiCredentialRequest request, CancellationToken cancellationToken)
    {
        if (request.Secret is null || request.Secret.Length is < 8 or > 2048 || request.Secret.Any(character => character is < '!' or > '~'))
        {
            throw new ArgumentException("Credencial inválida.", nameof(request));
        }

        return MutateAsync(tenantId, actorId, profileId, request.Revision, request.Secret, cancellationToken);
    }

    public Task<AiCredentialMutationResult> RemoveAsync(Guid tenantId, Guid actorId, string profileId, long revision, CancellationToken cancellationToken) =>
        MutateAsync(tenantId, actorId, profileId, revision, null, cancellationToken);

    private async Task<AiCredentialMutationResult> MutateAsync(Guid tenantId, Guid actorId, string profileId, long revision, string? secret, CancellationToken cancellationToken)
    {
        if (!protector.Available)
        {
            return new("protection_unavailable", null);
        }

        if (revision is < 1 or long.MaxValue)
        {
            return new("conflict", null);
        }

        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.Settings.SingleOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken);
        if (record is null || record.Revision != revision)
        {
            return new("conflict", null);
        }

        var catalog = JsonSerializer.Deserialize<AiProviderCatalog>(record.Payload)!;
        var profile = catalog.Profiles.SingleOrDefault(item => string.Equals(item.Id, profileId, StringComparison.Ordinal));
        if (profile is null)
        {
            return new("profile_not_found", null);
        }

        var credentials = AiCredentialEnvelope.Read(record.ProtectedCredentials);
        if (secret is null)
        {
            if (!credentials.Remove(profileId))
            {
                return new("ok", Status(record));
            }
        }
        else
        {
            try
            {
                credentials[profileId] = new(profile.Provider, protector.Protect(tenantId, profileId, profile.Provider, secret));
            }
            catch (Exception exception) when (exception is CryptographicException or IOException or UnauthorizedAccessException)
            {
                return new("protection_unavailable", null);
            }
        }

        record.ProtectedCredentials = AiCredentialEnvelope.Write(credentials);
        record.Revision++;
        record.UpdatedAt = DateTimeOffset.UtcNow;
        record.UpdatedBy = actorId;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new("conflict", null);
        }

        return new("ok", Status(record));
    }

    private AiCredentialStatus Status(AiSettingsRecord? record) => new(record?.Revision ?? 0, protector.Available,
        record is null ? [] : AiCredentialEnvelope.Read(record.ProtectedCredentials).Keys.Order(StringComparer.Ordinal).ToArray());
}
