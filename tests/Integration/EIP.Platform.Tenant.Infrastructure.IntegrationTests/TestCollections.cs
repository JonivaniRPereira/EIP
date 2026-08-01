using EIP.Testing.Infrastructure;
using Xunit;

namespace EIP.Platform.Tenant.Infrastructure.IntegrationTests;

/// <summary>xUnit resolve [CollectionDefinition] por assembly — precisa ser redeclarado aqui mesmo
/// a fixture (<see cref="SqlServerContainerFixture"/>) vivendo em EIP.Testing.Infrastructure.</summary>
[CollectionDefinition(SqlServerCollection.Name)]
public sealed class LocalSqlServerCollection : ICollectionFixture<SqlServerContainerFixture>;
