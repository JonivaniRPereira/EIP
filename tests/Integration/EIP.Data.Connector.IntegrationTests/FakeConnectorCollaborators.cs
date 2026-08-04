using EIP.Data.Canonical.Application;
using EIP.Data.Connector.Application.Abstractions;
using EIP.Data.DataLake;
using EIP.Data.Pipeline;
using EIP.Data.Warehouse.Application;

namespace EIP.Data.Connector.IntegrationTests;

/// <summary>Registra cada <paramref name="updatedSince"/> recebido, para que os testes de watermark
/// (E7.1/E7.2) verifiquem exatamente o que o processador passou adiante — sem depender de um
/// endpoint HTTP real. Sempre devolve o mesmo conteúdo fixo (JSON de um único cliente), já que estes
/// testes verificam a estratégia de watermark, não o mapeamento canônico (já coberto por
/// <c>EIP.Data.Pipeline.IntegrationTests</c>).</summary>
public sealed class RecordingReferenceRestClient : IReferenceRestClient
{
    public List<DateTimeOffset?> ReceivedUpdatedSince { get; } = [];

    public Task<byte[]> FetchRawContentAsync(string baseUrl, DateTimeOffset? updatedSince, CancellationToken cancellationToken)
    {
        ReceivedUpdatedSince.Add(updatedSince);
        return Task.FromResult("[]"u8.ToArray());
    }
}

/// <summary>Fixa o relatório do Pipeline em vez de processar o conteúdo de verdade — o mapeamento
/// canônico (E3) e a estratégia de watermark (E7.1) são preocupações independentes.</summary>
public sealed class FixedPipelineProcessor : IPipelineProcessor
{
    public Task<PipelineProcessingResult> ProcessAsync(PipelineProcessingRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new PipelineProcessingResult(ExtractedCount: 0, AcceptedCount: 0, UpdatedCount: 0, RejectedCount: 0));
}

/// <summary>Sem MinIO real nestes testes — só precisa devolver uma chave determinística, nunca é
/// lido de volta.</summary>
public sealed class NoOpRawObjectStore : IRawObjectStore
{
    public Task<StoredRawObject> PutAsync(RawObjectMetadata metadata, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
        Task.FromResult(new StoredRawObject($"test/{Guid.NewGuid():N}.json", Sha256Checksum: "test", SizeBytes: content.Length));

    public Task<Stream> GetAsync(Guid tenantId, string key, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Não usado nos testes de watermark.");

    public Task<IReadOnlyList<string>> ListKeysAsync(Guid tenantId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Não usado nos testes de watermark.");
}

/// <summary>Nunca invocado nestes testes: o bloco de reconciliação Canônico↔Origem/carga do
/// Warehouse só dispara para <c>SourceEntity == "sales-invoices"</c>
/// (<see cref="Application.ConnectorSyncProcessor"/>), e os testes de watermark usam
/// <c>"customers"</c> para isolar exatamente a estratégia de watermark.</summary>
public sealed class UnusedCanonicalReconciliationService : ICanonicalReconciliationService
{
    public Task<SalesInvoiceReconciliationResult> ReconcileSalesInvoicesAsync(
        Guid tenantId, Guid sourceSystemId, int reportedCount, decimal reportedNetAmountTotal, decimal toleranceFraction, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Não usado nos testes de watermark (SourceEntity != sales-invoices).");
}

public sealed class UnusedWarehouseLoadService : IWarehouseLoadService
{
    public Task<WarehouseLoadResult> LoadSalesInvoiceItemsAsync(Guid tenantId, Guid sourceSystemId, string correlationId, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Não usado nos testes de watermark (SourceEntity != sales-invoices).");
}

public sealed class UnusedWarehouseReconciliationService : IWarehouseReconciliationService
{
    public Task<CanonicalToFactReconciliationResult> ReconcileSalesInvoiceItemsAsync(
        Guid tenantId, Guid sourceSystemId, decimal toleranceFraction, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Não usado nos testes de watermark (SourceEntity != sales-invoices).");
}
