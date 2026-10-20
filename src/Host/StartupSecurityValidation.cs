using System.Text;
using Microsoft.Data.SqlClient;

namespace EIP.Host;

public static class StartupSecurityValidation
{
    public static void Validate(IConfiguration configuration, bool development)
    {
        if (development)
        {
            return;
        }

        var key = configuration["Jwt:SigningKey"] ?? string.Empty;
        if (Encoding.UTF8.GetByteCount(key) < 32 || key.Contains("Dev_Only", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Chave JWT de desenvolvimento ou insuficiente fora de Development.");
        }

        foreach (var entry in configuration.GetSection("ConnectionStrings").GetChildren().Where(c => !c.Key.StartsWith('_')))
        {
            if (string.IsNullOrWhiteSpace(entry.Value) || entry.Value.Contains("Dev_Only", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Configuração de conexão de desenvolvimento fora de Development.");
            }

            if (entry.Key.EndsWith("Db", StringComparison.Ordinal))
            {
                var sql = new SqlConnectionStringBuilder(entry.Value);
                if (string.Equals(sql.UserID, "sa", StringComparison.OrdinalIgnoreCase) || sql.TrustServerCertificate
                    || sql.Encrypt.ToString() is not ("True" or "Mandatory" or "Strict"))
                {
                    throw new InvalidOperationException("SQL exige identidade restrita e validação TLS fora de Development.");
                }
            }
        }

        var storageSecret = configuration["DataLake:SecretKey"];
        if (!Uri.TryCreate(configuration.GetConnectionString("RabbitMQ"), UriKind.Absolute, out var broker) || broker.Scheme != "amqps"
            || !StackExchange.Redis.ConfigurationOptions.Parse(configuration.GetConnectionString("Redis") ?? string.Empty).Ssl)
        {
            throw new InvalidOperationException("Broker e cache exigem TLS fora de Development.");
        }

        if (string.IsNullOrWhiteSpace(storageSecret) || storageSecret.Contains("Dev_Only", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Credencial de storage de desenvolvimento fora de Development.");
        }

        if (!Uri.TryCreate(configuration["DataLake:ServiceUrl"], UriKind.Absolute, out var storageUri) || storageUri.Scheme != "https")
        {
            throw new InvalidOperationException("Storage exige HTTPS fora de Development.");
        }

        if (configuration.GetSection("ReferenceRest:AllowedEndpoints").Get<string[]>()?.Any(url => !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) == true)
        {
            throw new InvalidOperationException("Conectores HTTP de desenvolvimento não são permitidos fora de Development.");
        }
    }
}
