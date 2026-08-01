namespace EIP.Platform.Identity.Application.Abstractions;

public static class AuditEventTypes
{
    public const string UserRegistered = "UserRegistered";
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string LoginLockedOut = "LoginLockedOut";
}

public interface IAuditLogger
{
    Task LogAsync(string eventType, Guid? userId, string? email, string? detail, CancellationToken cancellationToken);
}
