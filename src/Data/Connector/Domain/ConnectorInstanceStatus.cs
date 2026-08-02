namespace EIP.Data.Connector.Domain;

/// <summary>Ciclo de vida simplificado para o MVP (docs/05-Connector-Framework.md §5 define um ciclo
/// completo Draft→Configuring→Validating→Active↔Paused↔Error↔Disabled; a Fase 0 só precisa provar o
/// fluxo síncrono→assíncrono ponta a ponta com uma única instância de referência, então os estágios
/// de configuração/validação ficam para quando o Connector Registry completo existir, na Fase 1).</summary>
public enum ConnectorInstanceStatus
{
    Active,
    Paused,
}
