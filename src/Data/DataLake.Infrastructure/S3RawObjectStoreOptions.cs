namespace EIP.Data.DataLake.Infrastructure;

/// <summary>Configuração de acesso ao Object Storage S3-compatible (MinIO na Fase 0/1, ver
/// docs/03-Stack-Tecnologica.md §6.2). Nunca um segredo real fora do ambiente local
/// (docs/07-Seguranca.md §8) — mesmos valores "dev only" já públicos em
/// deploy/docker-compose/.env.example.</summary>
public sealed record S3RawObjectStoreOptions
{
    public const string SectionName = "DataLake";

    public required string ServiceUrl { get; init; }

    public required string AccessKey { get; init; }

    public required string SecretKey { get; init; }

    public required string BucketName { get; init; }
}
