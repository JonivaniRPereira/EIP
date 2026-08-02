namespace EIP.Data.Pipeline;

/// <summary>
/// Orquestra, por sincronização, a validação/mapeamento/resolução de referências/persistência do
/// Modelo Canônico a partir do conteúdo bruto extraído pelo conector (docs/04-Modelo-Canonico.md §7:
/// "Pipeline canônico responsável por: validar, normalizar, resolver referências, registrar
/// linhagem, publicar dado válido"). Framework-agnostic — não conhece RabbitMQ nem o módulo
/// Connector; quem o invoca (Worker/`ConnectorSyncProcessor`) decide o que fazer com o resultado.
/// </summary>
public interface IPipelineProcessor
{
    Task<PipelineProcessingResult> ProcessAsync(PipelineProcessingRequest request, CancellationToken cancellationToken);
}
