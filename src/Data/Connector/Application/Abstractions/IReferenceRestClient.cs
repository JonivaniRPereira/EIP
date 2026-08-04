namespace EIP.Data.Connector.Application.Abstractions;

/// <summary>
/// Cliente do único Connector Type de referência da Fase 0/1: REST API genérica
/// (docs/05-Connector-Framework.md §14). A implementação concreta (Infrastructure, via
/// <c>HttpClient</c>) faz a extração de fato. Retorna o conteúdo bruto exatamente como recebido —
/// nunca transformado aqui — para ser preservado no Data Lake antes de qualquer mapeamento
/// (docs/04-Modelo-Canonico.md §7: "conector responsável por extrair, preservar").
/// </summary>
public interface IReferenceRestClient
{
    /// <summary>Extração incremental (E7.1, docs/04-Modelo-Canonico.md §11): quando
    /// <paramref name="updatedSince"/> é informado, é repassado ao endpoint de origem como filtro de
    /// "atualizado desde" — nunca aplicado aqui no cliente (a origem decide o que retornar; um
    /// filtro local exigiria buscar tudo de qualquer forma, anulando o ganho da incrementalidade).</summary>
    Task<byte[]> FetchRawContentAsync(string baseUrl, DateTimeOffset? updatedSince, CancellationToken cancellationToken);
}
