using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Orders.Api.Persistence;

/// <summary>
/// Lets `dotnet ef` build the context without a live database or a running host.
/// It reads the same development connection string the service uses, so there is
/// no second copy of it to drift.
/// </summary>
internal sealed class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var options = new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Database"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new OrdersDbContext(options);
    }
}
