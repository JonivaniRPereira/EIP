using EIP.BuildingBlocks.DDD;

namespace EIP.Platform.Tenant.Domain;

/// <summary>Fronteira máxima de isolamento da plataforma (docs/08-Multi-Tenant.md §3, §4.1).</summary>
public sealed class Tenant : Entity<Guid>
{
    public string Name { get; private set; }
    public string Slug { get; private set; }
    public TenantStatus Status { get; private set; }
    public Guid PlanId { get; private set; }
    public DataIsolationMode DataIsolationMode { get; private set; }
    public string DefaultTimezone { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Tenant(Guid id, string name, string slug, Guid planId, string defaultTimezone)
        : base(id)
    {
        Name = name;
        Slug = slug;
        PlanId = planId;
        DefaultTimezone = defaultTimezone;
        Status = TenantStatus.Provisioning;
        DataIsolationMode = DataIsolationMode.Shared;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    private Tenant()
    {
        Name = string.Empty;
        Slug = string.Empty;
        DefaultTimezone = string.Empty;
    }

    public static Tenant Create(string name, string slug, Guid planId, string defaultTimezone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultTimezone);

        return new Tenant(Guid.NewGuid(), name, slug, planId, defaultTimezone);
    }

    public void Activate()
    {
        if (Status != TenantStatus.Provisioning)
        {
            throw new InvalidOperationException($"Tenant só pode ser ativado a partir de '{TenantStatus.Provisioning}' (status atual: '{Status}').");
        }

        Status = TenantStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
