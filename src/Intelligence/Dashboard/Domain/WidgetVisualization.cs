namespace EIP.Intelligence.Dashboard.Domain;

/// <summary>Configuração mínima de exibição por tipo de widget (docs/12 §5, reduzida ao MVP — sem
/// editor visual de posicionamento livre nem tema configurável). <see cref="Unit"/> é usado por
/// <see cref="WidgetType.Kpi"/> (ex. "R$", "%"); <see cref="Color"/> é usado por
/// <see cref="WidgetType.LineChart"/>. <see cref="WidgetType.Table"/> não precisa de nenhum campo
/// extra — colunas vêm diretamente de <c>QueryDefinition.Dimensions</c>/<c>Metrics</c>.</summary>
public sealed record WidgetVisualization(string? Unit = null, string? Color = null);
