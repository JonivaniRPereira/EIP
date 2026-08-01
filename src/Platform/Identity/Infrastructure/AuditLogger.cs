using EIP.Platform.Identity.Application.Abstractions;
using EIP.Platform.Identity.Domain;

namespace EIP.Platform.Identity.Infrastructure;

public sealed class AuditLogger : IAuditLogger
{
    private readonly AppIdentityDbContext _dbContext;

    public AuditLogger(AppIdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAsync(string eventType, Guid? userId, string? email, string? detail, CancellationToken cancellationToken)
    {
        _dbContext.AuditEvents.Add(AuditEvent.Create(eventType, userId, email, detail));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
