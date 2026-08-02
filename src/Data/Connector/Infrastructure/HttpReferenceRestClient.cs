using System.Net.Http;
using EIP.Data.Connector.Application.Abstractions;

namespace EIP.Data.Connector.Infrastructure;

/// <summary>
/// Implementação do único Connector Type de referência da Fase 0/1 — REST API genérica
/// (docs/05-Connector-Framework.md §14). Só extrai e devolve o conteúdo bruto — nunca interpreta ou
/// transforma o payload aqui (isso é responsabilidade do Pipeline, E3.3).
/// </summary>
public sealed class HttpReferenceRestClient : IReferenceRestClient
{
    public const string HttpClientName = "ReferenceRestConnector";

    private readonly IHttpClientFactory _httpClientFactory;

    public HttpReferenceRestClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<byte[]> FetchRawContentAsync(string baseUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(baseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
