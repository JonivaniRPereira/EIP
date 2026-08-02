using System.Net.Http;
using System.Text.Json;
using EIP.Data.Connector.Application.Abstractions;

namespace EIP.Data.Connector.Infrastructure;

/// <summary>
/// Implementação do único Connector Type de referência da Fase 0 — REST API genérica
/// (docs/05-Connector-Framework.md §14). Espera um array JSON na raiz da resposta e conta seus
/// elementos; persistir o conteúdo bruto com linhagem/checksum completos (Data Lake) é escopo da
/// Fase 1 — aqui o objetivo é só provar o fluxo síncrono→assíncrono ponta a ponta com um resultado
/// real e auditável (contagem de registros).
/// </summary>
public sealed class HttpReferenceRestClient : IReferenceRestClient
{
    public const string HttpClientName = "ReferenceRestConnector";

    private readonly IHttpClientFactory _httpClientFactory;

    public HttpReferenceRestClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<int> FetchRecordCountAsync(string baseUrl, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(baseUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Resposta do conector REST não é um array JSON.");
        }

        return document.RootElement.GetArrayLength();
    }
}
