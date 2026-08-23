namespace EIP.Intelligence.Dashboard.Domain;

/// <summary>Ciclo de vida do Dashboard (docs/12-Dashboard-Builder.md §4), reduzido ao MVP desta fase
/// (`docs/roadmap/fase-2-backlog.md` §3): sem o estado <c>InReview</c> — não existe processo de
/// aprovação modelado ainda em nenhum módulo da plataforma que justifique adicioná-lo.</summary>
public enum DashboardStatus
{
    Draft,
    Published,
    Archived,
}
