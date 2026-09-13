namespace OrdersSaga.IntegrationTests.Infrastructure;

/// <summary>
/// Every test that needs a database joins this collection, so the container and
/// the service hosts are built once per run instead of once per class.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SharedDatabase : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
