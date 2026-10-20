namespace EIP.Intelligence.AI.Application;

/// <summary>Configuração global da instalação. Segredos são referências a variáveis de ambiente, nunca valores.</summary>
public sealed class AiProviderOptions
{
    public string? ActiveProfileId { get; set; }
    public List<AiProviderProfileOptions> Profiles { get; set; } = [];
}

public sealed class AiProviderProfileOptions
{
    public string Id { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string CredentialEnvironmentVariable { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxOutputTokens { get; set; } = 1024;
}

public sealed record AiProviderProfile(string Id, string Provider, string Model, int TimeoutSeconds, int MaxOutputTokens);
public sealed record AiProviderCatalog(string? ActiveProfileId, bool InvocationEnabled, IReadOnlyList<AiProviderProfile> Profiles);

/// <summary>Snapshot validado e sem credenciais para descoberta dos perfis. Não habilita transportes de modelo.</summary>
public sealed class AiProviderConfiguration
{
    public AiProviderCatalog Catalog { get; }

    public AiProviderConfiguration(AiProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var profiles = new List<AiProviderProfile>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (options.Profiles is null || options.Profiles.Count > 20)
        {
            throw new ArgumentException("AI admite até 20 perfis de provedor.", nameof(options));
        }

        foreach (var profile in options.Profiles)
        {
            if (profile is null || !Identifier(profile.Id) || !Identifier(profile.Provider)
                || !ids.Add(profile.Id) || profile.Model is null || profile.Model.Length > 128
                || profile.TimeoutSeconds is < 1 or > 120 || profile.MaxOutputTokens is < 1 or > 32768
                || !CredentialReference(profile.CredentialEnvironmentVariable))
            {
                throw new ArgumentException("Perfil de IA inválido: verifique identificador, modelo, limites e referência de credencial.", nameof(options));
            }

            profiles.Add(new(profile.Id, profile.Provider, profile.Model, profile.TimeoutSeconds, profile.MaxOutputTokens));
        }

        if (options.ActiveProfileId is not null && !ids.Contains(options.ActiveProfileId))
        {
            throw new ArgumentException("O perfil ativo de IA deve existir na lista de perfis.", nameof(options));
        }

        Catalog = new(options.ActiveProfileId, false, profiles.AsReadOnly());
    }

    private static bool Identifier(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 64
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');

    private static bool CredentialReference(string value) => !string.IsNullOrEmpty(value)
        && value.StartsWith("EIP_AI_", StringComparison.Ordinal) && value.Length <= 128
        && value.All(character => character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_');
}
