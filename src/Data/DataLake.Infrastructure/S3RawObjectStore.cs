using System.Globalization;
using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;

namespace EIP.Data.DataLake.Infrastructure;

/// <summary>
/// Implementação de <see cref="IRawObjectStore"/> via cliente S3-compatible (MinIO na Fase 0/1).
/// Convenção de chave obrigatória (docs/roadmap/fase-1-backlog.md E1.2):
/// <c>{tenantId}/{sourceSystemId}/{sourceEntity}/{yyyy}/{MM}/{dd}/{syncRunId}/{sequencial}.json</c>
/// — sempre construída aqui a partir de <see cref="RawObjectMetadata"/>, nunca aceita do chamador.
/// </summary>
public sealed class S3RawObjectStore : IRawObjectStore, IDisposable
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly SemaphoreSlim _bucketEnsureLock = new(1, 1);
    private bool _bucketEnsured;

    public S3RawObjectStore(IAmazonS3 s3Client, string bucketName)
    {
        _s3Client = s3Client;
        _bucketName = bucketName;
    }

    public void Dispose() => _bucketEnsureLock.Dispose();

    public async Task<StoredRawObject> PutAsync(RawObjectMetadata metadata, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        if (metadata.TenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId é obrigatório.", nameof(metadata));
        }

        await EnsureBucketExistsAsync(cancellationToken);

        var checksum = Convert.ToHexStringLower(SHA256.HashData(content.Span));
        var key = BuildKey(metadata);

        using var stream = new MemoryStream(content.ToArray(), writable: false);
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = stream,
            ContentType = "application/json",
        };
        request.Metadata.Add("tenant-id", metadata.TenantId.ToString());
        request.Metadata.Add("source-system-id", metadata.SourceSystemId.ToString());
        request.Metadata.Add("source-entity", metadata.SourceEntity);
        request.Metadata.Add("sha256-checksum", checksum);
        if (metadata.ConnectorInstanceId is { } connectorInstanceId)
        {
            request.Metadata.Add("connector-instance-id", connectorInstanceId.ToString());
        }

        if (metadata.SyncRunId is { } syncRunId)
        {
            request.Metadata.Add("sync-run-id", syncRunId.ToString());
        }

        await _s3Client.PutObjectAsync(request, cancellationToken);

        return new StoredRawObject(key, checksum, content.Length);
    }

    public async Task<Stream> GetAsync(Guid tenantId, string key, CancellationToken cancellationToken)
    {
        EnsureKeyBelongsToTenant(tenantId, key);

        var response = await _s3Client.GetObjectAsync(_bucketName, key, cancellationToken);
        return response.ResponseStream;
    }

    public async Task<IReadOnlyList<string>> ListKeysAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        // O prefixo é o próprio filtro nativo do Object Storage — não uma checagem posterior. Uma
        // chave de outro tenant fisicamente não aparece no resultado (docs/09-Data-Warehouse.md §11).
        var prefix = TenantPrefix(tenantId);
        var keys = new List<string>();
        string? continuationToken = null;

        do
        {
            var response = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = prefix,
                ContinuationToken = continuationToken,
            }, cancellationToken);

            keys.AddRange(response.S3Objects.Select(o => o.Key));
            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return keys;
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        if (_bucketEnsured)
        {
            return;
        }

        await _bucketEnsureLock.WaitAsync(cancellationToken);
        try
        {
            if (_bucketEnsured)
            {
                return;
            }

            if (!await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _bucketName))
            {
                await _s3Client.PutBucketAsync(_bucketName, cancellationToken);
            }

            _bucketEnsured = true;
        }
        finally
        {
            _bucketEnsureLock.Release();
        }
    }

    private static string BuildKey(RawObjectMetadata metadata)
    {
        var ingestedAt = metadata.IngestedAt;
        var syncRunSegment = metadata.SyncRunId?.ToString("D", CultureInfo.InvariantCulture) ?? "no-sync-run";

        // "Sequencial" simplificado como um sufixo único aleatório: o Object Storage não tem uma
        // sequência transacional embutida, e um GUID curto evita colisão sem precisar de um serviço
        // de contador externo.
        var sequence = Guid.NewGuid().ToString("N")[..12];

        return string.Create(CultureInfo.InvariantCulture, $"{metadata.TenantId:D}/{metadata.SourceSystemId:D}/{metadata.SourceEntity}/{ingestedAt:yyyy}/{ingestedAt:MM}/{ingestedAt:dd}/{syncRunSegment}/{sequence}.json");
    }

    private static void EnsureKeyBelongsToTenant(Guid tenantId, string key)
    {
        if (!key.StartsWith(TenantPrefix(tenantId), StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Chave do Data Lake não pertence ao tenant informado.");
        }
    }

    private static string TenantPrefix(Guid tenantId) => string.Create(CultureInfo.InvariantCulture, $"{tenantId:D}/");
}
