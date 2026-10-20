using System.Text;
using System.Text.Json;
using EIP.Intelligence.AI.Application;
using EIP.Intelligence.AI.Infrastructure;
using FluentAssertions;

namespace EIP.Host.IntegrationTests;

public sealed class AiProviderProtocolTests
{
    [Theory]
    [InlineData("openai", "api.openai.com", "Authorization")]
    [InlineData("anthropic", "api.anthropic.com", "x-api-key")]
    [InlineData("google", "generativelanguage.googleapis.com", "x-goog-api-key")]
    public async Task RequestsUseOfficialHostAndExcludeTenantAndCredentialFromBody(string provider, string host, string header)
    {
        var tenant = Guid.NewGuid();
        using var request = AiProviderProtocol.CreateRequest(new("test", provider, "test-model", 30, 512),
            "synthetic-test-value", new(tenant, "Receita?", []));
        request.RequestUri!.Host.Should().Be(host);
        request.RequestUri.Query.Should().BeEmpty();
        request.Headers.Contains(header).Should().BeTrue();
        var body = await request.Content!.ReadAsStringAsync();
        body.Should().NotContain(tenant.ToString()).And.NotContain("synthetic-test-value");
        using var json = JsonDocument.Parse(body);
        if (provider == "openai")
        {
            json.RootElement.GetProperty("store").GetBoolean().Should().BeFalse();
        }
    }

    [Theory]
    [InlineData("openai")]
    [InlineData("anthropic")]
    [InlineData("google")]
    public void ParsesClarificationAndDeclarativeQuery(string provider)
    {
        Parse(provider, """{"clarification":"Qual período?"}""").Clarification.Should().Be("Qual período?");
        var result = Parse(provider, """{"query":{"dataset":"sales","metrics":["netRevenue"],"dimensions":["date.month"]}}""");
        result.ErrorCode.Should().BeNull();
        result.Query!.Dataset.Should().Be("sales");
    }

    [Theory]
    [InlineData("{\"query\":{\"dataset\":\"sales\",\"metrics\":[\"netRevenue\"],\"dimensions\":[\"date.month\"],\"tenantId\":\"other\"}}")]
    [InlineData("{\"sql\":\"SELECT * FROM tenants\"}")]
    [InlineData("{\"clarification\":\"a\",\"clarification\":\"b\"}")]
    [InlineData("{\"clarification\":\" \"}")]
    [InlineData("not json")]
    public void RejectsUntrustedProposalShapes(string proposal)
    {
        foreach (var provider in new[] { "openai", "anthropic", "google" })
        {
            Parse(provider, proposal).ErrorCode.Should().Be("ai_provider_invalid_response");
        }
    }

    [Theory]
    [InlineData("openai", "{\"status\":\"incomplete\",\"output\":[]}")]
    [InlineData("openai", "{\"status\":\"completed\",\"output\":[{\"type\":\"function_call\"}]}")]
    [InlineData("anthropic", "{\"stop_reason\":\"max_tokens\",\"content\":[]}")]
    [InlineData("google", "{\"candidates\":[{\"finishReason\":\"SAFETY\"}]}")]
    [InlineData("google", "{\"candidates\":[]}")]
    public void RejectsIncompleteBlockedAndToolResponses(string provider, string body) =>
        AiProviderProtocol.ParseResponse(provider, Encoding.UTF8.GetBytes(body)).ErrorCode.Should().NotBeNull();

    [Fact]
    public void BoundsInputAndOutputAndRejectsModelPathInjection()
    {
        var input = new AiModelRequest(Guid.NewGuid(), "Receita?", []);
        Action invalidModel = () => AiProviderProtocol.CreateRequest(new("p", "google", "../other?key=bad", 30, 512), "synthetic-value", input);
        invalidModel.Should().Throw<ArgumentException>();
        Action oversizedInput = () => AiProviderProtocol.CreateRequest(new("p", "openai", "model", 30, 512), "synthetic-value", input with { Question = new string('x', 4097) });
        oversizedInput.Should().Throw<ArgumentException>();
        AiProviderProtocol.ParseResponse("openai", new byte[65537]).ErrorCode.Should().NotBeNull();
    }

    private static AiModelProposal Parse(string provider, string text)
    {
        object envelope = provider switch
        {
            "openai" => new { status = "completed", output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } } },
            "anthropic" => new { stop_reason = "end_turn", content = new[] { new { type = "text", text } } },
            _ => new { candidates = new[] { new { finishReason = "STOP", content = new { parts = new[] { new { text } } } } } }
        };
        return AiProviderProtocol.ParseResponse(provider, JsonSerializer.SerializeToUtf8Bytes(envelope));
    }
}
