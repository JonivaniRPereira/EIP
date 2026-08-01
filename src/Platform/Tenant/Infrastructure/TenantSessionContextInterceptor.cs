using System.Data;
using System.Data.Common;
using EIP.BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EIP.Platform.Tenant.Infrastructure;

/// <summary>
/// Define o SESSION_CONTEXT('TenantId') do SQL Server logo após cada abertura de conexão, a partir
/// do contexto de tenant autenticado (nunca de input do cliente). É o mecanismo concreto exigido
/// pela ADR-007 para que a RLS (SECURITY POLICY) do banco filtre as linhas automaticamente.
///
/// sp_reset_connection (chamado pelo ADO.NET ao reutilizar uma conexão do pool) limpa o
/// SESSION_CONTEXT anterior, então cada "ConnectionOpened" corresponde sempre a um estado limpo —
/// por isso é seguro usar @read_only = 1 (impede que código da própria requisição sobrescreva o
/// TenantId no meio da operação).
/// </summary>
public sealed class TenantSessionContextInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContextAccessor _tenantContextAccessor;

    public TenantSessionContextInterceptor(ITenantContextAccessor tenantContextAccessor)
    {
        _tenantContextAccessor = tenantContextAccessor;
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        base.ConnectionOpened(connection, eventData);

        using var command = CreateSetSessionContextCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);

        await using var command = CreateSetSessionContextCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateSetSessionContextCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "EXEC sp_set_session_context @key = N'TenantId', @value = @TenantId, @read_only = 1;";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "@TenantId";
        parameter.DbType = DbType.Guid;
        parameter.Value = (object?)_tenantContextAccessor.Current?.TenantId ?? DBNull.Value;
        command.Parameters.Add(parameter);

        return command;
    }
}
