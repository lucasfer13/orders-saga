using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventory.Api.Persistence;

/// <summary>
/// Lets `dotnet ef` build the context without a live database or a running host.
/// It reads the same development connection string the service uses, so there is
/// no second copy of it to drift.
/// </summary>
internal sealed class InventoryDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Database"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new InventoryDbContext(options);
    }
}
