namespace EIP.Platform.Connector.Application.Abstractions;

/// <summary>
/// Cliente do único Connector Type de referência da Fase 0: REST API genérica
/// (docs/05-Connector-Framework.md §14). A implementação concreta (Infrastructure, via
/// <c>HttpClient</c>) faz a extração de fato; aqui a Application só conhece o resultado agregado
/// (contagem de registros) — persistir dado bruto/linhagem completa é escopo da Fase 1 (Data Lake).
/// </summary>
public interface IReferenceRestClient
{
    Task<int> FetchRecordCountAsync(string baseUrl, CancellationToken cancellationToken);
}
