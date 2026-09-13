using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Payments.Api.Persistence;

/// <summary>
/// Lets `dotnet ef` build the context without a live database or a running host.
/// It reads the same development connection string the service uses, so there is
/// no second copy of it to drift.
/// </summary>
internal sealed class PaymentsDbContextFactory : IDesignTimeDbContextFactory<PaymentsDbContext>
{
    public PaymentsDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.Development.json")
            .Build();

        var options = new DbContextOptionsBuilder<PaymentsDbContext>()
            .UseNpgsql(configuration.GetConnectionString("Database"))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new PaymentsDbContext(options);
    }
}
