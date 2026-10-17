using EIP.Data.Connector.Application.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace EIP.Host;

public sealed class ConnectorSyncQuotaOptions
{
    public int PermitLimit { get; set; } = 1;

    public int WindowSeconds { get; set; } = 60;
}

/// <summary>Janela fixa iniciada na primeira solicitação aceita. Lua mantém a reserva atômica
/// entre processos da API; rejeições não renovam a janela. Não usa cache Get/Set não atômico.</summary>
public sealed class RedisConnectorSyncQuota : IConnectorSyncQuota
{
    private const string AcquireScript = """
        local count = tonumber(redis.call('GET', KEYS[1]) or '0')
        if count >= tonumber(ARGV[1]) then
            return math.max(1, redis.call('PTTL', KEYS[1]))
        end
        count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        return 0
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly ConnectorSyncQuotaOptions _options;

    public RedisConnectorSyncQuota(IConnectionMultiplexer redis, IOptions<ConnectorSyncQuotaOptions> options)
    {
        _redis = redis;
        _options = options.Value;
    }

    public async Task<TimeSpan?> TryAcquireAsync(Guid tenantId, Guid connectorInstanceId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = await _redis.GetDatabase().ScriptEvaluateAsync(
            AcquireScript,
            [$"connector:sync-quota:{tenantId:N}:{connectorInstanceId:N}"],
            [_options.PermitLimit, (long)_options.WindowSeconds * 1000]);
        var milliseconds = (long)result;
        return milliseconds == 0 ? null : TimeSpan.FromMilliseconds(milliseconds);
    }
}
