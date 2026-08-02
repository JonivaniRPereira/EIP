using EIP.Data.Connector.Application.Contracts;

namespace EIP.Data.Connector.Application.Abstractions;

/// <summary>Publica a solicitação de sincronização na fila assíncrona. A implementação concreta
/// (RabbitMQ) fica na Infrastructure — a Application não conhece exchange/queue/routing key
/// (docs/02-Arquitetura.md §9.2).</summary>
public interface IConnectorSyncPublisher
{
    Task PublishAsync(SyncRequestedMessage message, CancellationToken cancellationToken);
}
