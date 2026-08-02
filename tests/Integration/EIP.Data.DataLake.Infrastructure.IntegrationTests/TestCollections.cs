using EIP.Testing.Infrastructure;
using Xunit;

namespace EIP.Data.DataLake.Infrastructure.IntegrationTests;

/// <summary>xUnit resolve [CollectionDefinition] por assembly — precisa ser redeclarado aqui mesmo
/// a fixture (<see cref="MinioContainerFixture"/>) vivendo em EIP.Testing.Infrastructure.</summary>
[CollectionDefinition(MinioCollection.Name)]
public sealed class LocalMinioCollection : ICollectionFixture<MinioContainerFixture>;
