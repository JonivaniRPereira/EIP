namespace EIP.Data.DataLake;

public sealed record RawStorageUsage(long Bytes, long Objects);

public interface IRawStorageUsageReader
{
    /// <summary>Objetos atualmente presentes, modificados no intervalo [from, to). Não reconstrói
    /// snapshots históricos de objetos removidos e não inclui versões antigas.</summary>
    Task<RawStorageUsage> ReadUsageAsync(Guid tenantId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
