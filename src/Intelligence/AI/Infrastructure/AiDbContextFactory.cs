using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EIP.Intelligence.AI.Infrastructure;

public sealed class AiDbContextFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public AiDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<AiDbContext>()
        .UseSqlServer(Environment.GetEnvironmentVariable("EIP_AI_DB_CONNECTION")
            ?? throw new InvalidOperationException("Defina EIP_AI_DB_CONNECTION para executar migrations de IA."),
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", AiDbContext.Schema)).Options);
}
