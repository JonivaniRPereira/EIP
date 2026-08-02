using EIP.Testing.Infrastructure;
using Xunit;

namespace EIP.Data.Warehouse.IntegrationTests;

/// <summary>xUnit resolve [CollectionDefinition] por assembly — precisa ser redeclarado aqui mesmo a
/// fixture (<see cref="SqlServerContainerFixture"/>) vivendo em EIP.Testing.Infrastructure.</summary>
[CollectionDefinition(SqlServerCollection.Name)]
public sealed class LocalSqlServerCollection : ICollectionFixture<SqlServerContainerFixture>;
