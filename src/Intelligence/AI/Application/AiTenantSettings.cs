using System.Text.Json.Serialization;

namespace EIP.Intelligence.AI.Application;

public sealed record AiTenantSettings(long Revision, string? ActiveProfileId,
    IReadOnlyList<AiProviderProfile> Profiles, bool InvocationEnabled, DateTimeOffset? UpdatedAt);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveAiSettingsRequest(long Revision, string? ActiveProfileId, IReadOnlyList<AiProviderProfile> Profiles);

public interface IAiSettingsStore
{
    Task<AiTenantSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<AiTenantSettings?> SaveAsync(Guid tenantId, Guid actorId, SaveAiSettingsRequest request, CancellationToken cancellationToken);
}

public static class AiTenantSettingsValidation
{
    public static void Validate(SaveAiSettingsRequest request, AiProviderConfiguration defaults)
    {
        if (request.Revision < 0 || request.Revision == long.MaxValue || request.Profiles is null
            || request.Profiles.Count > 20 || request.Profiles.Any(profile => profile is null))
        {
            throw new ArgumentException("Configuração de IA inválida.");
        }

        var providers = defaults.Catalog.Profiles.Select(profile => profile.Provider).ToHashSet(StringComparer.Ordinal);
        if (request.Profiles.Any(profile => !providers.Contains(profile.Provider)))
        {
            throw new ArgumentException("Selecione um provedor disponível na plataforma.");
        }

        _ = new AiProviderConfiguration(new AiProviderOptions
        {
            ActiveProfileId = request.ActiveProfileId,
            Profiles = request.Profiles.Select(profile => new AiProviderProfileOptions
            {
                Id = profile.Id,
                Provider = profile.Provider,
                Model = profile.Model,
                TimeoutSeconds = profile.TimeoutSeconds,
                MaxOutputTokens = profile.MaxOutputTokens,
                CredentialEnvironmentVariable = "EIP_AI_UNCONFIGURED",
            }).ToList(),
        });

        if (request.ActiveProfileId is { } active
            && string.IsNullOrWhiteSpace(request.Profiles.Single(profile => profile.Id == active).Model))
        {
            throw new ArgumentException("Informe o modelo do perfil selecionado.");
        }
    }
}
