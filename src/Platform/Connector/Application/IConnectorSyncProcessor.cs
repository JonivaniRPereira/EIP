using EIP.Platform.Connector.Application.Contracts;

namespace EIP.Platform.Connector.Application;

/// <summary>Processa uma <see cref="SyncRequestedMessage"/> consumida do RabbitMQ pelo worker
/// (E7.2). Framework-agnostic: o worker (composition root) só chama isto e decide, a partir de uma
/// exceção propagada, se a mensagem deve ser rejeitada para a DLQ (docs/05-Connector-Framework.md
/// §10.3) — esta classe não conhece RabbitMQ.</summary>
public interface IConnectorSyncProcessor
{
    Task ProcessAsync(SyncRequestedMessage message, CancellationToken cancellationToken);
}
