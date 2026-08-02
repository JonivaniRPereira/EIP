using System.Globalization;
using System.Text.Json;
using EIP.Data.Canonical.Application;
using EIP.Data.Canonical.Domain;
using EIP.Shared.Contracts.Canonical;

namespace EIP.Data.Pipeline;

/// <summary>
/// Implementação de referência do Pipeline (E3.3) para a fatia Comercial. Mapeamento fixo no
/// código, não configurável — não existe Connector Registry completo ainda
/// (docs/roadmap/fase-1-backlog.md §3). Cada registro do lote é processado isoladamente: uma falha
/// de validação em um registro nunca aborta os demais (docs/04-Modelo-Canonico.md §8.2 — quarentena).
/// </summary>
public sealed class PipelineProcessor : IPipelineProcessor
{
    private const string SchemaVersion = "1.0";

    private readonly ICanonicalRecordStore _store;

    public PipelineProcessor(ICanonicalRecordStore store)
    {
        _store = store;
    }

    public async Task<PipelineProcessingResult> ProcessAsync(PipelineProcessingRequest request, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(request.RawContent);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Conteúdo bruto do conector não é um array JSON.");
        }

        var records = document.RootElement.EnumerateArray().ToList();
        var accepted = 0;
        var rejected = 0;

        foreach (var record in records)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ProcessRecordAsync(request, record, cancellationToken);
                accepted++;
            }
            catch (CanonicalValidationException ex)
            {
                rejected++;
                var entry = CanonicalQuarantineEntry.Create(
                    request.TenantId,
                    request.SourceSystemId,
                    syncRunId: request.SyncRunId,
                    sourceEntity: request.SourceEntity,
                    rawObjectUri: request.RawObjectUri,
                    correlationId: request.CorrelationId,
                    failedRule: ex.Rule,
                    reason: ex.Message);
                await _store.AddQuarantineEntryAsync(entry, cancellationToken);
            }
        }

        return new PipelineProcessingResult(records.Count, accepted, rejected);
    }

    private Task ProcessRecordAsync(PipelineProcessingRequest request, JsonElement record, CancellationToken cancellationToken) =>
        request.SourceEntity switch
        {
            CanonicalSourceEntities.Customers => ProcessCustomerAsync(request, record, cancellationToken),
            CanonicalSourceEntities.Products => ProcessProductAsync(request, record, cancellationToken),
            CanonicalSourceEntities.SalesInvoices => ProcessSalesInvoiceAsync(request, record, cancellationToken),
            _ => throw new CanonicalValidationException("unknown-source-entity", $"SourceEntity '{request.SourceEntity}' não é reconhecido pelo pipeline."),
        };

    private async Task ProcessCustomerAsync(PipelineProcessingRequest request, JsonElement record, CancellationToken cancellationToken)
    {
        var code = GetRequiredString(record, "code");
        var name = GetRequiredString(record, "name");
        var isActive = GetOptionalBool(record, "isActive", defaultValue: true);
        var email = GetOptionalString(record, "email");
        var city = GetOptionalString(record, "city");
        var stateOrRegion = GetOptionalString(record, "stateOrRegion");
        var countryCode = GetOptionalString(record, "countryCode");

        var customer = Customer.Create(BuildLineage(request, code), code, name, isActive, taxId: null, email, city, stateOrRegion, countryCode);
        await _store.UpsertCustomerAsync(customer, cancellationToken);
    }

    private async Task ProcessProductAsync(PipelineProcessingRequest request, JsonElement record, CancellationToken cancellationToken)
    {
        var code = GetRequiredString(record, "code");
        var name = GetRequiredString(record, "name");
        var isActive = GetOptionalBool(record, "isActive", defaultValue: true);
        var unitOfMeasure = GetOptionalString(record, "unitOfMeasure");
        var productType = GetOptionalString(record, "productType") switch
        {
            "Product" => ProductType.Product,
            "Service" => ProductType.Service,
            _ => ProductType.Other,
        };

        var product = Product.Create(BuildLineage(request, code), code, name, productType, isActive, categoryId: null, unitOfMeasure);
        await _store.UpsertProductAsync(product, cancellationToken);
    }

    private async Task ProcessSalesInvoiceAsync(PipelineProcessingRequest request, JsonElement record, CancellationToken cancellationToken)
    {
        var invoiceNumber = GetRequiredString(record, "invoiceNumber");
        var issueDateRaw = GetRequiredString(record, "issueDate");
        if (!DateOnly.TryParse(issueDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var issueDate))
        {
            throw new CanonicalValidationException("invalid-date", $"issueDate inválida: '{issueDateRaw}'.");
        }

        var customerCode = GetRequiredString(record, "customerCode");
        var currencyCode = GetRequiredString(record, "currencyCode");
        var status = GetOptionalString(record, "status") switch
        {
            "Issued" => SalesInvoiceStatus.Issued,
            "Canceled" => SalesInvoiceStatus.Canceled,
            _ => SalesInvoiceStatus.Unknown,
        };

        if (!record.TryGetProperty("items", out var itemsElement)
            || itemsElement.ValueKind != JsonValueKind.Array
            || itemsElement.GetArrayLength() == 0)
        {
            throw new CanonicalValidationException("missing-items", "Fatura sem itens — pelo menos um item é obrigatório.");
        }

        // Referência não resolvida vai para quarentena, nunca uma relação arbitrária (docs/04 §6.3).
        var customer = await _store.FindCustomerByCodeAsync(request.TenantId, customerCode, cancellationToken)
            ?? throw new CanonicalValidationException("unresolved-reference", $"Cliente com código '{customerCode}' não encontrado no Modelo Canônico — sincronize clientes antes de faturas.");

        var parsedItems = new List<(int LineNumber, Guid ProductId, decimal Quantity, decimal UnitPrice, decimal DiscountAmount, decimal Gross, decimal Net)>();
        foreach (var itemElement in itemsElement.EnumerateArray())
        {
            var lineNumber = GetRequiredInt(itemElement, "lineNumber");
            var productCode = GetRequiredString(itemElement, "productCode");
            var quantity = GetRequiredDecimal(itemElement, "quantity");
            var unitPrice = GetRequiredDecimal(itemElement, "unitPrice");
            var discountAmount = GetOptionalDecimal(itemElement, "discountAmount", defaultValue: 0m);

            var product = await _store.FindProductByCodeAsync(request.TenantId, productCode, cancellationToken)
                ?? throw new CanonicalValidationException("unresolved-reference", $"Produto com código '{productCode}' não encontrado no Modelo Canônico — sincronize produtos antes de faturas.");

            var gross = quantity * unitPrice;
            var net = gross - discountAmount;
            parsedItems.Add((lineNumber, product.Id, quantity, unitPrice, discountAmount, gross, net));
        }

        var totalGross = parsedItems.Sum(i => i.Gross);
        var totalDiscount = parsedItems.Sum(i => i.DiscountAmount);
        var totalNet = parsedItems.Sum(i => i.Net);

        var invoice = SalesInvoice.Create(
            BuildLineage(request, invoiceNumber),
            invoiceNumber,
            issueDate,
            customer.Id,
            status,
            currencyCode,
            totalGross,
            totalDiscount,
            totalNet);

        var items = parsedItems
            .Select(i => SalesInvoiceItem.Create(
                BuildLineage(request, $"{invoiceNumber}-{i.LineNumber}"),
                invoice.Id,
                i.LineNumber,
                i.ProductId,
                i.Quantity,
                i.UnitPrice,
                i.DiscountAmount,
                i.Gross,
                i.Net))
            .ToList();

        await _store.UpsertSalesInvoiceAsync(invoice, items, cancellationToken);
    }

    private static CanonicalLineage BuildLineage(PipelineProcessingRequest request, string sourceRecordId) =>
        new(
            request.TenantId,
            request.CompanyId,
            request.SourceSystemId,
            request.SourceEntity,
            sourceRecordId,
            SourceUpdatedAt: null,
            SchemaVersion,
            request.CorrelationId,
            request.RawObjectUri);

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new CanonicalValidationException("required-field", $"Campo obrigatório ausente ou inválido: '{propertyName}'.");
        }

        return value.GetString()!;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool GetOptionalBool(JsonElement element, string propertyName, bool defaultValue) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : defaultValue;

    private static decimal GetRequiredDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var result))
        {
            throw new CanonicalValidationException("required-field", $"Campo numérico obrigatório ausente ou inválido: '{propertyName}'.");
        }

        return result;
    }

    private static decimal GetOptionalDecimal(JsonElement element, string propertyName, decimal defaultValue) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var result)
            ? result
            : defaultValue;

    private static int GetRequiredInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
        {
            throw new CanonicalValidationException("required-field", $"Campo inteiro obrigatório ausente ou inválido: '{propertyName}'.");
        }

        return result;
    }
}
