using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

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

        // UseSetting, not ConfigureAppConfiguration: every Program.cs reads its
        // connection string off builder.Configuration before calling Build(), and
        // configuration sources added here are only merged in by Build(). With
        // ConfigureAppConfiguration the override arrived too late and the service
        // silently fell back to appsettings.Development.json — which points at the
        // compose database, so the suite passed locally and failed everywhere else.
        foreach (var (key, value) in overrides)
        {
            builder.UseSetting(key, value);
        }
    }
}
