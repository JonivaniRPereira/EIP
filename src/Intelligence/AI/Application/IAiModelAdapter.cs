using EIP.Intelligence.Analytics.Application;

namespace EIP.Intelligence.AI.Application;

public sealed record AiModelStatus(bool Enabled, string Code, string? Provider, string? Model);

/// <summary>Entrada mínima para propor consulta: catálogo, pergunta e contexto autenticado; sem fatos ou credenciais.</summary>
public sealed record AiModelRequest(Guid TenantId, string Question, IReadOnlyList<AiDataset> Datasets);

/// <summary>Uma proposta nunca executa consulta; o chamador deve validar pelo Analytics autorizado.</summary>
public sealed record AiModelProposal(AnalyticsQueryDefinition? Query, string? Clarification, string? ErrorCode);

public interface IAiModelAdapter
{
    AiModelStatus Status { get; }

    Task<AiModelProposal> ProposeAsync(AiModelRequest request, CancellationToken cancellationToken);
}

/// <summary>Único adaptador registrado até definir provedor, política de dados e orçamento. Sem rede ou persistência.</summary>
public sealed class DisabledAiModelAdapter : IAiModelAdapter
{
    public const string DisabledCode = "ai_model_not_configured";

    public AiModelStatus Status { get; } = new(false, DisabledCode, null, null);

    public Task<AiModelProposal> ProposeAsync(AiModelRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new AiModelProposal(null, null, DisabledCode));
    }
}
