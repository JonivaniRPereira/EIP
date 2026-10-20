using System.Net.Http;
using EIP.Data.Connector.Application.Abstractions;
using Microsoft.Extensions.Options;

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
    private readonly IReferenceEndpointPolicy _policy;
    private readonly int _maxResponseBytes;

    public HttpReferenceRestClient(IHttpClientFactory httpClientFactory, IReferenceEndpointPolicy policy, IOptions<ReferenceRestOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _policy = policy;
        _maxResponseBytes = options.Value.MaxResponseBytes;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(_maxResponseBytes);
    }

    public async Task<byte[]> FetchRawContentAsync(string baseUrl, DateTimeOffset? updatedSince, CancellationToken cancellationToken)
    {
        if (!_policy.IsAllowed(baseUrl))
        {
            throw new InvalidOperationException("Destino de conector não autorizado pelo operador.");
        }

        var requestUrl = updatedSince is { } since
            ? AppendUpdatedSinceQueryParam(baseUrl, since)
            : baseUrl;

        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.GetAsync(requestUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if ((int)response.StatusCode is >= 300 and < 400)
        {
            throw new InvalidOperationException("Redirecionamento de conector não permitido.");
        }
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > _maxResponseBytes)
        {
            throw new InvalidOperationException("Resposta de conector excede o limite de tamanho.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, timeout.Token)) > 0)
        {
            if (buffer.Length + read > _maxResponseBytes)
            {
                throw new InvalidOperationException("Resposta de conector excede o limite de tamanho.");
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    private static string AppendUpdatedSinceQueryParam(string baseUrl, DateTimeOffset updatedSince)
    {
        var separator = baseUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        var encodedValue = Uri.EscapeDataString(updatedSince.ToString("O"));
        return $"{baseUrl}{separator}updatedSince={encodedValue}";
    }
}
