using System.Text.Json;
using EIP.Intelligence.AI.Application;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EIP.Intelligence.AI.Infrastructure;

public sealed class AiSettingsStore(IDbContextFactory<AiDbContext> factory, AiProviderConfiguration defaults) : IAiSettingsStore
{
    public async Task<AiTenantSettings> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.Settings.AsNoTracking().SingleOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken);
        return record is null ? new(0, defaults.Catalog.ActiveProfileId, defaults.Catalog.Profiles, false, null) : View(record);
    }

    public async Task<AiTenantSettings?> SaveAsync(Guid tenantId, Guid actorId, SaveAiSettingsRequest request, CancellationToken cancellationToken)
    {
        AiTenantSettingsValidation.Validate(request, defaults);
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var record = await db.Settings.SingleOrDefaultAsync(row => row.TenantId == tenantId, cancellationToken);
        if ((record?.Revision ?? 0) != request.Revision)
        {
            return null;
        }

        if (record is null)
        {
            record = new AiSettingsRecord { TenantId = tenantId };
            db.Settings.Add(record);
        }

        record.Revision = request.Revision + 1;
        var credentials = AiCredentialEnvelope.Read(record.ProtectedCredentials);
        foreach (var entry in credentials.ToArray())
        {
            if (!request.Profiles.Any(profile => profile.Id == entry.Key && profile.Provider == entry.Value.Provider))
            {
                credentials.Remove(entry.Key);
            }
        }

        record.ProtectedCredentials = AiCredentialEnvelope.Write(credentials);
        record.Payload = JsonSerializer.Serialize(new AiProviderCatalog(request.ActiveProfileId, false, request.Profiles));
        record.UpdatedAt = DateTimeOffset.UtcNow;
        record.UpdatedBy = actorId;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return null;
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return null;
        }

        return View(record);
    }

    private static AiTenantSettings View(AiSettingsRecord record)
    {
        var catalog = JsonSerializer.Deserialize<AiProviderCatalog>(record.Payload)!;
        return new(record.Revision, catalog.ActiveProfileId, catalog.Profiles, false, record.UpdatedAt);
    }
}
