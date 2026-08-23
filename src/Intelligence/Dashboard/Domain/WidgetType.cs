namespace EIP.Intelligence.Dashboard.Domain;

/// <summary>3 tipos de widget no MVP (docs/12 §5 lista 7; reduzido em `docs/roadmap/fase-2-backlog.md`
/// §3 — estes 3 cobrem os 3 KPIs certificados da Fase 1 sem precisar de Bar/Donut/Filter-control
/// ainda).</summary>
public enum WidgetType
{
    Kpi,
    LineChart,
    Table,
}
