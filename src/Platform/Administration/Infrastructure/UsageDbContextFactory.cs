using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EIP.Platform.Administration.Infrastructure;

public sealed class UsageDbContextFactory : IDesignTimeDbContextFactory<UsageDbContext>
{
    public UsageDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("EIP_USAGE_DB_CONNECTION")
            ?? "Server=localhost,1433;Database=EIP;User Id=sa;Password=Dev_OnlyChangeMe_123!;TrustServerCertificate=True;";
        return new UsageDbContext(new DbContextOptionsBuilder<UsageDbContext>()
            .UseSqlServer(connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", UsageDbContext.Schema)).Options);
    }
}
