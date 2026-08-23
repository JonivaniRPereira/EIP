namespace EIP.Intelligence.Analytics.Application;

/// <summary>
/// Guardrails mínimos do Analytics Engine (Fase 2, E1.5, `docs/10-Analytics-Engine.md` §8.2 reduzido —
/// `docs/roadmap/fase-2-backlog.md` §3): sem quotas por plano/usuário (não existe conceito de "plano"
/// na plataforma ainda) nem exportação assíncrona como job — só limite de linhas de resposta, período
/// máximo de filtro de data e timeout de execução. Consulta que excede qualquer um retorna erro claro
/// (`docs/06-API-Design.md` §10, Problem Details), nunca trunca silenciosamente.
/// </summary>
public sealed record AnalyticsGuardrailOptions(int MaxRows, int MaxPeriodDays, TimeSpan QueryTimeout);
