using EIP.Host;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace EIP.Host.IntegrationTests;

public sealed class StartupSecurityValidationTests
{
    [Theory]
    [InlineData("Jwt:SigningKey", "Dev_OnlyChangeMe_SigningKey_MustBeLongEnough_123!")]
    [InlineData("Jwt:SigningKey", "short")]
    [InlineData("ConnectionStrings:TenantDb", "Server=sql;Database=EIP;User Id=sa;Password=example;Encrypt=True;")]
    [InlineData("ConnectionStrings:TenantDb", "Server=sql;Database=EIP;User Id=eip_app;Password=example;Encrypt=True;TrustServerCertificate=True;")]
    [InlineData("ConnectionStrings:TenantDb", "Server=sql;Database=EIP;User Id=eip_app;Password=example;Encrypt=False;")]
    [InlineData("DataLake:SecretKey", "Dev_OnlyChangeMe_123!")]
    [InlineData("DataLake:ServiceUrl", "http://storage:9000")]
    [InlineData("ReferenceRest:AllowedEndpoints:0", "http://localhost/sample")]
    [InlineData("ConnectionStrings:RabbitMQ", "amqp://operator:example@broker/")]
    [InlineData("ConnectionStrings:Redis", "redis:6380,ssl=false")]
    public void OutsideDevelopment_UnsafeSettingsPreventStartup(string key, string value)
    {
        var settings = SafeSettings();
        settings[key] = value;
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var action = () => StartupSecurityValidation.Validate(config, development: false);
        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExplicitSettingsPass_AndDevelopmentRemainsUsable()
    {
        StartupSecurityValidation.Validate(new ConfigurationBuilder().AddInMemoryCollection(SafeSettings()).Build(), false);
        StartupSecurityValidation.Validate(new ConfigurationBuilder().Build(), true);
    }

    private static Dictionary<string, string?> SafeSettings() => new()
    {
        ["Jwt:SigningKey"] = "TestOnly_OperatorProvidedKey_32CharactersOrMore!",
        ["ConnectionStrings:TenantDb"] = "Server=sql;Database=EIP;User Id=eip_app;Password=example;Encrypt=True;TrustServerCertificate=False;",
        ["DataLake:SecretKey"] = "test-only-operator-supplied-storage-key",
        ["DataLake:ServiceUrl"] = "https://storage.example.test",
        ["ReferenceRest:AllowedEndpoints:0"] = "https://erp.example.test/customers",
        ["ConnectionStrings:RabbitMQ"] = "amqps://operator:example@broker/",
        ["ConnectionStrings:Redis"] = "redis:6380,ssl=true",
    };
}
