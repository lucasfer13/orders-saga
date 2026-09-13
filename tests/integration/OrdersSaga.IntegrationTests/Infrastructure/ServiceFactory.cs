using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace OrdersSaga.IntegrationTests.Infrastructure;

/// <summary>
/// Hosts one service against the shared PostgreSQL container. The service keeps
/// its own startup path, so the schema is created the same way it is in production.
/// </summary>
public sealed class ServiceFactory<TEntryPoint>(string connectionString, IReadOnlyDictionary<string, string?>? settings = null)
    : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        var overrides = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Database"] = connectionString,
        };

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>(StringComparer.Ordinal))
        {
            overrides[key] = value;
        }

        // Added last so it wins over the service's own appsettings files.
        builder.ConfigureAppConfiguration(configuration => configuration.AddInMemoryCollection(overrides));
    }
}
