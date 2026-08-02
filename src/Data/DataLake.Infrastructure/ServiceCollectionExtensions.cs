using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;

namespace EIP.Data.DataLake.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registra o cliente S3-compatible apontando para o MinIO (ou qualquer endpoint
    /// S3-compatible) e o <see cref="IRawObjectStore"/>. <c>ForcePathStyle = true</c> é obrigatório
    /// para MinIO (endpoints virtual-hosted-style não são suportados por padrão).</summary>
    public static IServiceCollection AddRawObjectStore(this IServiceCollection services, S3RawObjectStoreOptions options)
    {
        services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
            new BasicAWSCredentials(options.AccessKey, options.SecretKey),
            new AmazonS3Config
            {
                ServiceURL = options.ServiceUrl,
                ForcePathStyle = true,
            }));

        services.AddSingleton<IRawObjectStore>(sp => new S3RawObjectStore(sp.GetRequiredService<IAmazonS3>(), options.BucketName));

        return services;
    }
}
