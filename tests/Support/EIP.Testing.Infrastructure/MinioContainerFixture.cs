using EIP.Data.DataLake;
using EIP.Data.DataLake.Infrastructure;
using Testcontainers.Minio;
using Xunit;

namespace EIP.Testing.Infrastructure;

/// <summary>
/// Dependência efêmera para testes de integração do Data Lake (docs/roadmap/fase-1-backlog.md E1.2/
/// E1.3) — mesma filosofia do <see cref="SqlServerContainerFixture"/>: sobe um MinIO real isolado
/// por execução de teste (mesma imagem do E1 da Fase 0), sem depender do `docker compose` local
/// estar de pé nem em CI.
/// </summary>
public sealed class MinioContainerFixture : IAsyncLifetime
{
    private const string BucketName = "eip-datalake-tests";

    private readonly MinioContainer _container = new MinioBuilder("minio/minio:RELEASE.2025-04-08T15-41-24Z")
        .WithUsername("eip_dev")
        .WithPassword("Dev_OnlyChangeMe_123!")
        .Build();

    public IRawObjectStore Store { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var options = new S3RawObjectStoreOptions
        {
            ServiceUrl = _container.GetConnectionString(),
            AccessKey = "eip_dev",
            SecretKey = "Dev_OnlyChangeMe_123!",
            BucketName = BucketName,
        };

        Store = new S3RawObjectStore(
            new Amazon.S3.AmazonS3Client(
                new Amazon.Runtime.BasicAWSCredentials(options.AccessKey, options.SecretKey),
                new Amazon.S3.AmazonS3Config { ServiceURL = options.ServiceUrl, ForcePathStyle = true }),
            options.BucketName);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

/// <summary>Um único container compartilhado entre todas as classes de teste desta coleção — mesmo
/// motivo do <see cref="SqlServerCollection"/>.</summary>
[CollectionDefinition(Name)]
public sealed class MinioCollection : ICollectionFixture<MinioContainerFixture>
{
    public const string Name = "Minio";
}
