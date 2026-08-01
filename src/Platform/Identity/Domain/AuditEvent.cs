using EIP.BuildingBlocks.DDD;

namespace EIP.Platform.Identity.Domain;

/// <summary>Auditoria mínima de identidade (docs/07-Seguranca.md §11.2): login, falha de login e
/// alterações de credenciais. Não é protegida por RLS — não é dado de tenant, é log de plataforma.</summary>
public sealed class AuditEvent : Entity<Guid>
{
    public DateTimeOffset OccurredAt { get; private set; }
    public string EventType { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Email { get; private set; }
    public string? Detail { get; private set; }

    private AuditEvent(Guid id, string eventType, Guid? userId, string? email, string? detail)
        : base(id)
    {
        EventType = eventType;
        UserId = userId;
        Email = email;
        Detail = detail;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    private AuditEvent()
    {
        EventType = string.Empty;
    }

    public static AuditEvent Create(string eventType, Guid? userId, string? email, string? detail = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        return new AuditEvent(Guid.NewGuid(), eventType, userId, email, detail);
    }
}
