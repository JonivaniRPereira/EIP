using System.Security.Cryptography;
using System.Text;
using EIP.Data.DataLake;
using EIP.Testing.Infrastructure;
using FluentAssertions;

namespace EIP.Data.DataLake.Infrastructure.IntegrationTests;

/// <summary>
/// Prova, contra um MinIO real (Testcontainers, docs/roadmap/fase-1-backlog.md E1.2/E1.3), que o
/// Data Lake bruto grava/lê com integridade (checksum) e que o isolamento de tenant é aplicado de
/// verdade — não RLS (Object Storage não é SQL), mas o mesmo princípio: nunca confiar em um
/// TenantId/chave não validado (ADR-007, docs/09-Data-Warehouse.md §11).
/// </summary>
[Collection(MinioCollection.Name)]
public sealed class RawObjectStoreTests
{
    private readonly IRawObjectStore _store;

    public RawObjectStoreTests(MinioContainerFixture fixture)
    {
        _store = fixture.Store;
    }

    [Fact]
    public async Task PutAsync_ThenGetAsync_RoundTripsContentWithMatchingChecksum()
    {
        var tenantId = Guid.NewGuid();
        var content = Encoding.UTF8.GetBytes("""{"customerCode":"C001","name":"Cliente Teste"}""");
        var metadata = new RawObjectMetadata(tenantId, Guid.NewGuid(), "customers", ConnectorInstanceId: null, SyncRunId: Guid.NewGuid(), DateTimeOffset.UtcNow);

        var stored = await _store.PutAsync(metadata, content, CancellationToken.None);

        stored.Key.Should().StartWith($"{tenantId:D}/");
        stored.Sha256Checksum.Should().Be(Convert.ToHexStringLower(SHA256.HashData(content)));

        await using var readBack = await _store.GetAsync(tenantId, stored.Key, CancellationToken.None);
        using var reader = new MemoryStream();
        await readBack.CopyToAsync(reader);

        reader.ToArray().Should().Equal(content);
    }

    [Fact]
    public async Task ListKeysAsync_OnlyReturnsKeysBelongingToTheRequestedTenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        var storedA = await _store.PutAsync(
            new RawObjectMetadata(tenantA, Guid.NewGuid(), "customers", null, Guid.NewGuid(), DateTimeOffset.UtcNow),
            "a"u8.ToArray(),
            CancellationToken.None);

        var storedB = await _store.PutAsync(
            new RawObjectMetadata(tenantB, Guid.NewGuid(), "customers", null, Guid.NewGuid(), DateTimeOffset.UtcNow),
            "b"u8.ToArray(),
            CancellationToken.None);

        var keysForTenantA = await _store.ListKeysAsync(tenantA, CancellationToken.None);

        keysForTenantA.Should().Contain(storedA.Key);
        keysForTenantA.Should().NotContain(storedB.Key);
    }

    [Fact]
    public async Task GetAsync_WithKeyBelongingToAnotherTenant_ThrowsUnauthorizedAccessException()
    {
        var tenantB = Guid.NewGuid();
        var storedB = await _store.PutAsync(
            new RawObjectMetadata(tenantB, Guid.NewGuid(), "customers", null, Guid.NewGuid(), DateTimeOffset.UtcNow),
            "segredo-do-tenant-b"u8.ToArray(),
            CancellationToken.None);

        var tenantA = Guid.NewGuid();

        // Usuário do tenant A tentando ler a chave do tenant B (adulterada/vazada de alguma forma) —
        // tem que ser bloqueado antes de qualquer chamada real ao Object Storage.
        var act = async () => await _store.GetAsync(tenantA, storedB.Key, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
