namespace EIP.Data.Connector.Application.Abstractions;

/// <summary>Reserva atomicamente uma solicitação por tenant/conector antes de persistir ou publicar.
/// Retorna null quando aceita, ou o tempo restante da janela quando a quota está esgotada.</summary>
public interface IConnectorSyncQuota
{
    Task<TimeSpan?> TryAcquireAsync(Guid tenantId, Guid connectorInstanceId, CancellationToken cancellationToken);
}
