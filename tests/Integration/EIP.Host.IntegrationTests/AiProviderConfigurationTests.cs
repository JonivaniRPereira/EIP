using EIP.Intelligence.AI.Application;
using FluentAssertions;

namespace EIP.Host.IntegrationTests;

public sealed class AiProviderConfigurationTests
{
    [Fact]
    public void SeveralProfilesCanUseDifferentProvidersOrModelsWithoutEnablingCalls()
    {
        var options = new AiProviderOptions
        {
            ActiveProfileId = "alternate",
            Profiles = [Profile("primary", "vendor-a", "model-one"), Profile("alternate", "vendor-b", "model-two"),
                Profile("other-model", "vendor-a", "model-three")],
        };
        var configuration = new AiProviderConfiguration(options);
        options.Profiles.Clear();
        configuration.Catalog.ActiveProfileId.Should().Be("alternate");
        configuration.Catalog.Profiles.Should().HaveCount(3);
        configuration.Catalog.Profiles[1].Model.Should().Be("model-two");
        configuration.Catalog.InvocationEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown-active")]
    [InlineData("timeout")]
    [InlineData("tokens")]
    [InlineData("credential-value")]
    [InlineData("missing-provider")]
    public void InvalidProfilesAreRejectedBeforeUse(string scenario)
    {
        var profile = Profile("primary", "vendor-a", "model-one");
        var options = new AiProviderOptions { Profiles = [profile] };
        switch (scenario)
        {
            case "duplicate": options.Profiles.Add(Profile("primary", "vendor-b", "model-two")); break;
            case "unknown-active": options.ActiveProfileId = "missing"; break;
            case "timeout": profile.TimeoutSeconds = 121; break;
            case "tokens": profile.MaxOutputTokens = 0; break;
            case "credential-value": profile.CredentialEnvironmentVariable = "literal credential must never be accepted"; break;
            case "missing-provider": profile.Provider = string.Empty; break;
        }

        var configure = () => new AiProviderConfiguration(options);
        configure.Should().Throw<ArgumentException>();
    }

    private static AiProviderProfileOptions Profile(string id, string provider, string model) => new()
    {
        Id = id,
        Provider = provider,
        Model = model,
        CredentialEnvironmentVariable = "EIP_AI_TEST_CREDENTIAL",
    };
}
