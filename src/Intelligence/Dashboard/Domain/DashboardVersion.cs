using EIP.BuildingBlocks.DDD;

namespace EIP.Intelligence.Dashboard.Domain;

/// <summary>
/// Versão imutável de um <see cref="Dashboard"/> (docs/12 §4) — nunca editada após criada; publicar
/// de novo sempre gera uma nova versão, nunca sobrescreve a anterior (E2.2). <see cref="Widgets"/> é
/// uma lista de Value Objects serializada, não uma tabela própria — mesma razão de
/// <see cref="Widget"/> não ter identidade própria. Carrega seu próprio <see cref="TenantId"/> (nunca
/// só via FK para <see cref="Dashboard"/>): RLS neste projeto sempre compara a coluna própria da
/// tabela contra <c>SESSION_CONTEXT</c>, nunca um join (ADR-007, mesmo padrão de toda tabela
/// tenant-scoped desde a Fase 0). Protegida por RLS obrigatória.
///
/// <see cref="PublishedByUserId"/> (E5.3, fechamento da Fase 2) — cada linha desta tabela já é, por
/// construção, um registro imutável e append-only de toda publicação (docs/15 §6: "publicação...são
/// auditados"); faltava só registrar QUEM publicou, não só quando/o quê. Sem tabela de auditoria
/// própria: a própria história de versões já cumpre esse papel.
/// </summary>
public sealed class DashboardVersion : Entity<Guid>
{
    public Guid TenantId { get; private set; }

    public Guid DashboardId { get; private set; }

    public IReadOnlyList<Widget> Widgets { get; private set; }

    public Guid PublishedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private DashboardVersion(Guid id, Guid tenantId, Guid dashboardId, IReadOnlyList<Widget> widgets, Guid publishedByUserId)
        : base(id)
    {
        TenantId = tenantId;
        DashboardId = dashboardId;
        Widgets = widgets;
        PublishedByUserId = publishedByUserId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    private DashboardVersion()
    {
        Widgets = [];
    }

    public static DashboardVersion Create(Guid tenantId, Guid dashboardId, IReadOnlyList<Widget> widgets, Guid publishedByUserId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        if (dashboardId == Guid.Empty)
        {
            throw new ArgumentException("DashboardId é obrigatório.", nameof(dashboardId));
        }

        if (publishedByUserId == Guid.Empty)
        {
            throw new ArgumentException("PublishedByUserId é obrigatório.", nameof(publishedByUserId));
        }

        EnsureWidgetsValid(widgets);

        return new DashboardVersion(Guid.NewGuid(), tenantId, dashboardId, [.. widgets], publishedByUserId);
    }

    /// <summary>Validação contra o catálogo do Analytics Engine (E2.3) — nunca aceita uma consulta
    /// inválida num rascunho que será publicado. Extraída de <see cref="Create"/> para
    /// <see cref="Dashboard.UpdateDraft"/> poder validar o rascunho sem precisar construir (e
    /// descartar) uma versão completa, já que só <see cref="Dashboard.Publish"/> sabe quem está
    /// publicando.</summary>
    public static void EnsureWidgetsValid(IReadOnlyList<Widget> widgets)
    {
        if (widgets is not { Count: > 0 })
        {
            throw new ArgumentException("Uma versão precisa conter ao menos 1 widget.", nameof(widgets));
        }

        foreach (var widget in widgets)
        {
            WidgetQueryDefinitionValidator.EnsureValid(widget);
        }
    }
}
