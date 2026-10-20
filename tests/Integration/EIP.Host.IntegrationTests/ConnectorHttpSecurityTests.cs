using System.Net;
using EIP.Data.Connector.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Host.IntegrationTests;

public sealed class ConnectorHttpSecurityTests
{
    private const string Allowed = "https://erp.example.test/api/customers";

    [Fact]
    public async Task ActualHttpClientRegistration_DoesNotFollowRedirects()
    {
        var privateRequests = 0;
        using var server = new WebHostBuilder().UseKestrel().UseUrls("http://127.0.0.1:0")
            .Configure(app => app.Run(context =>
            {
                if (context.Request.Path == "/allowed")
                {
                    context.Response.StatusCode = 302;
                    context.Response.Headers.Location = "/private";
                }
                else
                {
                    Interlocked.Increment(ref privateRequests);
                }

                return Task.CompletedTask;
            })).Build();
        await server.StartAsync();
        var address = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var options = Options.Create(new ReferenceRestOptions { AllowedEndpoints = [$"{address}/allowed"] });
        using var services = new ServiceCollection().AddReferenceRestHttpClient().BuildServiceProvider();
        var connector = new HttpReferenceRestClient(services.GetRequiredService<IHttpClientFactory>(), new ReferenceEndpointPolicy(options), options);
        var action = async () => await connector.FetchRawContentAsync($"{address}/allowed", null, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>();
        privateRequests.Should().Be(0);
    }

    [Theory]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("https://erp.example.test.evil.test/api/customers")]
    [InlineData("https://erp.example.test/api/customers/../admin")]
    [InlineData("https://erp.example.test/api/customers?redirect=http://127.0.0.1")]
    [InlineData("https://user:password@erp.example.test/api/customers")]
    [InlineData("https://erp.example.test/api/customers#fragment")]
    public async Task UnapprovedUrl_NeverReachesHttpHandler(string url)
    {
        using var handler = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.OK));
        using var client = new HttpClient(handler);
        var service = Create(client, 32);
        var action = async () => await service.FetchRawContentAsync(url, null, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found)]
    [InlineData(HttpStatusCode.TemporaryRedirect)]
    public async Task Redirect_IsNotAccepted(HttpStatusCode status)
    {
        using var handler = new RecordingHandler(() => new HttpResponseMessage(status) { Headers = { Location = new Uri("http://169.254.169.254/") } });
        using var client = new HttpClient(handler);
        var action = async () => await Create(client, 32).FetchRawContentAsync(Allowed, null, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>();
        handler.Calls.Should().Be(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OversizedBody_WithOrWithoutLength_IsRejected(bool knownLength)
    {
        using var handler = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = knownLength ? new ByteArrayContent(new byte[33]) : new StreamContent(new NonSeekableStream(new byte[33])),
        });
        using var client = new HttpClient(handler);
        var action = async () => await Create(client, 32).FetchRawContentAsync(Allowed, null, CancellationToken.None);
        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ApprovedUrl_ExtractsWithinLimit_AndPreservesWatermark()
    {
        using var handler = new RecordingHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[]") });
        using var client = new HttpClient(handler);
        var bytes = await Create(client, 32).FetchRawContentAsync(Allowed, DateTimeOffset.UtcNow, CancellationToken.None);
        bytes.Should().Equal("[]"u8.ToArray());
        handler.LastUri!.Query.Should().Contain("updatedSince=");
    }

    private static HttpReferenceRestClient Create(HttpClient client, int maxBytes)
    {
        var options = Options.Create(new ReferenceRestOptions { AllowedEndpoints = [Allowed], MaxResponseBytes = maxBytes });
        return new HttpReferenceRestClient(new FixedFactory(client), new ReferenceEndpointPolicy(options), options);
    }

    private sealed class FixedFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public Uri? LastUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            LastUri = request.RequestUri;
            return Task.FromResult(response());
        }
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}
