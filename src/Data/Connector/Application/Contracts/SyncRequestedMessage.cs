namespace EIP.Data.Connector.Application.Contracts;

/// <summary>
/// Contrato de mensagem publicado no RabbitMQ quando uma sincronização é solicitada
/// (docs/03-Stack-Tecnologica.md §7.1: toda mensagem carrega <c>TenantId</c>, correlação e versão de
/// contrato explícitas). <see cref="ContractVersion"/> é versionado independentemente do código:
/// mudanças que quebrem compatibilidade exigem uma nova versão, nunca alterar esta silenciosamente.
///
/// O worker NUNCA confia cegamente no <see cref="TenantId"/> da mensagem (docs/05-Connector-Framework.md
/// §12) — ele o usa para popular o <c>SESSION_CONTEXT</c> (mesmo mecanismo de RLS de qualquer request
/// autenticado) e, além disso, valida explicitamente que a <see cref="ConnectorInstanceId"/> carregada
/// realmente pertence a esse tenant antes de processar (defesa em profundidade, mesmo padrão do
/// endpoint de referência <c>GET /api/v1/tenants/{tenantId}</c> do E2.5).
/// </summary>
public sealed record SyncRequestedMessage(
    Guid SyncRunId,
    Guid ConnectorInstanceId,
    Guid TenantId,
    string CorrelationId,
    string ContractVersion,
    DateTimeOffset? ReprocessFromUtc = null)
{
    public const string CurrentContractVersion = "1.0";
}
