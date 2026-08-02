using EIP.BuildingBlocks.Data;
using EIP.BuildingBlocks.Security;
using EIP.Platform.Tenant.Domain;
using EIP.Platform.Tenant.Infrastructure;
using EIP.Testing.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EIP.Platform.Tenant.Infrastructure.IntegrationTests;

/// <summary>
/// Prova, contra um SQL Server real (via EF Core, exatamente como em produção — não SQL bruto), que
/// a RLS obrigatória da ADR-007 realmente isola dados entre tenants: sem contexto autenticado nenhuma
/// linha é visível, um tenant nunca enxerga linhas de outro mesmo forçando o filtro manualmente, e uma
/// tentativa de gravar dado de outro tenant é bloqueada pelo banco (não apenas pela aplicação).
/// Exigido por docs/08-Multi-Tenant.md §13 e é o gate mínimo de conclusão do épico E2.
///
/// Roda contra um SQL Server efêmero (Testcontainers, docs/03 §3) — nada de infraestrutura
/// pré-existente precisa estar de pé para este teste passar, nem local nem no CI (E5).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class TenantIsolationTests : IAsyncLifetime
{
    private readonly string _connectionString;
    private readonly AsyncLocalTenantContextAccessor _tenantContextAccessor = new();
    private Domain.Tenant _tenantA = null!;
    private Domain.Tenant _tenantB = null!;

    public TenantIsolationTests(SqlServerContainerFixture sqlServerFixture)
    {
        _connectionString = sqlServerFixture.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        _tenantA = Domain.Tenant.Create("Tenant A (teste)", $"tenant-a-{Guid.NewGuid()}", Guid.NewGuid(), "America/Sao_Paulo");
        _tenantB = Domain.Tenant.Create("Tenant B (teste)", $"tenant-b-{Guid.NewGuid()}", Guid.NewGuid(), "America/Sao_Paulo");

        // Tenants não carregam TenantId (não são protegidos por RLS): sempre visíveis/gravável.
        await using (var db = CreateDbContext())
        {
            db.Tenants.AddRange(_tenantA, _tenantB);
            await db.SaveChangesAsync();
        }

        _tenantContextAccessor.Current = new TenantContext(_tenantA.Id);
        await using (var dbA = CreateDbContext())
        {
            dbA.Companies.Add(Company.Create(_tenantA.Id, "Empresa da A", "BRL"));
            await dbA.SaveChangesAsync();
        }

        _tenantContextAccessor.Current = new TenantContext(_tenantB.Id);
        await using (var dbB = CreateDbContext())
        {
            dbB.Companies.Add(Company.Create(_tenantB.Id, "Empresa da B", "BRL"));
            await dbB.SaveChangesAsync();
        }

        _tenantContextAccessor.Current = null;
    }

    public async Task DisposeAsync()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA.Id);
        await using (var dbA = CreateDbContext())
        {
            await dbA.Companies.Where(c => c.TenantId == _tenantA.Id).ExecuteDeleteAsync();
        }

        _tenantContextAccessor.Current = new TenantContext(_tenantB.Id);
        await using (var dbB = CreateDbContext())
        {
            await dbB.Companies.Where(c => c.TenantId == _tenantB.Id).ExecuteDeleteAsync();
        }

        _tenantContextAccessor.Current = null;
        await using (var db = CreateDbContext())
        {
            await db.Tenants.Where(t => t.Id == _tenantA.Id || t.Id == _tenantB.Id).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task Query_WithoutTenantContext_ReturnsNoRows()
    {
        _tenantContextAccessor.Current = null;
        await using var db = CreateDbContext();

        var companies = await db.Companies.ToListAsync();

        companies.Should().BeEmpty("sem SESSION_CONTEXT a política deve negar por padrão");
    }

    [Fact]
    public async Task Query_WithTenantAContext_ReturnsOnlyTenantACompanies()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA.Id);
        await using var db = CreateDbContext();

        var companies = await db.Companies.ToListAsync();

        companies.Should().ContainSingle().Which.TenantId.Should().Be(_tenantA.Id);
    }

    [Fact]
    public async Task Query_WithTenantAContext_NeverReturnsTenantBRows_EvenWithExplicitFilter()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA.Id);
        await using var db = CreateDbContext();

        // Mesmo pedindo explicitamente os dados do Tenant B, a RLS do banco bloqueia — o isolamento
        // não depende de o código da aplicação "lembrar" de filtrar corretamente.
        var companies = await db.Companies.Where(c => c.TenantId == _tenantB.Id).ToListAsync();

        companies.Should().BeEmpty();
    }

    [Fact]
    public async Task Query_WithSystemContext_ReturnsRowsAcrossAllTenants()
    {
        // A sentinela de sistema (TenantContext.System) é usada por código interno de confiança
        // (ex.: MembershipDirectory no login) para consultas legitimamente cross-tenant — nunca
        // atribuível a partir de input de cliente.
        _tenantContextAccessor.Current = TenantContext.System;
        await using var db = CreateDbContext();

        var companies = await db.Companies.ToListAsync();

        companies.Should().HaveCount(2);
        companies.Select(c => c.TenantId).Should().BeEquivalentTo([_tenantA.Id, _tenantB.Id]);
    }

    [Fact]
    public async Task Insert_WithMismatchedTenantContext_IsBlockedByRowLevelSecurity()
    {
        _tenantContextAccessor.Current = new TenantContext(_tenantA.Id);
        await using var db = CreateDbContext();

        db.Companies.Add(Company.Create(_tenantB.Id, "Empresa invasora", "BRL"));

        var act = async () => await db.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>("o block predicate da RLS rejeita gravação com TenantId divergente do SESSION_CONTEXT");
    }

    private TenantDbContext CreateDbContext()
    {
        var interceptor = new TenantSessionContextInterceptor(_tenantContextAccessor);
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseSqlServer(_connectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new TenantDbContext(options);
    }
}
