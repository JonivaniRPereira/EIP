using EIP.BuildingBlocks.DDD;

namespace EIP.Intelligence.Dashboard.Domain;

/// <summary>
/// Um painel do Dashboard Builder (Fase 2, E2.1, docs/12-Dashboard-Builder.md §3) — escopado só por
/// <see cref="TenantId"/> (Workspace continua fora do escopo, mesma decisão das Fases 0/1). Protegida
/// por RLS obrigatória (ADR-007). <see cref="Widgets"/> é o RASCUNHO atual (editável, mutável — E3.1's
/// PATCH edita exatamente isto); <see cref="DashboardVersion"/> é o histórico imutável de instantâneos
/// tirados a cada publicação (E2.2) — nunca a mesma coisa que o rascunho em edição.
/// </summary>
public sealed class Dashboard : Entity<Guid>
{
    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    public Guid OwnerId { get; private set; }

    public DashboardStatus Status { get; private set; }

    public IReadOnlyList<Widget> Widgets { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private Dashboard(Guid id, Guid tenantId, string name, Guid ownerId)
        : base(id)
    {
        TenantId = tenantId;
        Name = name;
        OwnerId = ownerId;
        Status = DashboardStatus.Draft;
        Widgets = [];
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private Dashboard()
    {
        Name = string.Empty;
        Widgets = [];
    }

    public static Dashboard Create(Guid tenantId, string name, Guid ownerId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(tenantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (ownerId == Guid.Empty)
        {
            throw new ArgumentException("OwnerId é obrigatório.", nameof(ownerId));
        }

        return new Dashboard(Guid.NewGuid(), tenantId, name, ownerId);
    }

    /// <summary>
    /// Edita o rascunho (E3.1's `PATCH /api/v1/dashboards/{id}`) — <paramref name="name"/> e
    /// <paramref name="widgets"/> são independentemente opcionais (<see langword="null"/> = não mexe
    /// naquele campo). Cada widget é validado contra o catálogo do Analytics Engine antes de aceitar
    /// (E2.3, via <see cref="DashboardVersion.Create"/> reaproveitado só para validar — o resultado
    /// nunca é persistido como versão, é descartado, só os widgets validados são guardados aqui).
    /// Editar um dashboard <see cref="DashboardStatus.Published"/> sempre volta para
    /// <see cref="DashboardStatus.Draft"/> — a versão já publicada permanece intacta em
    /// <see cref="DashboardVersion"/>, nunca sobrescrita (docs/12 §4, E2.2).
    /// </summary>
    public void UpdateDraft(string? name, IReadOnlyList<Widget>? widgets)
    {
        if (name is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
            Name = name;
        }

        if (widgets is not null)
        {
            DashboardVersion.EnsureWidgetsValid(widgets);
            Widgets = [.. widgets];
        }

        if (Status == DashboardStatus.Published)
        {
            Status = DashboardStatus.Draft;
        }

        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Publica o dashboard (E2.2, docs/12 §4): promove <see cref="Status"/> para
    /// <see cref="DashboardStatus.Published"/> (idempotente quanto ao status — publicar de novo mantém
    /// Published) e sempre cria e devolve um instantâneo novo e imutável do rascunho atual
    /// (<see cref="Widgets"/>) em <see cref="DashboardVersion"/>, nunca sobrescrevendo os instantâneos
    /// anteriores — cada chamada é uma entrada nova no histórico, incluindo
    /// <paramref name="publishedByUserId"/> (E5.3: "publicação...são auditados", docs/15 §6). A
    /// atomicidade (Dashboard atualizado + nova versão inserida na mesma transação) é responsabilidade
    /// de quem persiste (um único <c>SaveChangesAsync</c> cobrindo as duas mudanças), não deste método
    /// puro de domínio.
    /// </summary>
    public DashboardVersion Publish(Guid publishedByUserId)
    {
        if (Widgets.Count == 0)
        {
            throw new InvalidOperationException("Não é possível publicar um dashboard sem widgets no rascunho.");
        }

        Status = DashboardStatus.Published;
        UpdatedAt = DateTimeOffset.UtcNow;

        return DashboardVersion.Create(TenantId, Id, Widgets, publishedByUserId);
    }

    /// <summary>Arquiva o dashboard (E3.1's `POST /{id}/archive`) — histórico de versões publicadas
    /// permanece intacto e consultável, só o ciclo de vida muda.</summary>
    public void Archive()
    {
        Status = DashboardStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
