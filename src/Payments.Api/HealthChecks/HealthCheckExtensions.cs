using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Payments.Api.Persistence;

namespace Payments.Api.HealthChecks;

internal static class HealthCheckExtensions
{
    private const string Live = "live";
    private const string Ready = "ready";
    private const string Startup = "startup";
    private const string Postgres = "postgres";

    public static IServiceCollection AddServiceHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [Live], timeout: TimeSpan.FromSeconds(1))
            // Probes through the very DbContext the service uses: passing means the
            // application can work, not just that a socket opened.
            .AddDbContextCheck<PaymentsDbContext>(Postgres, tags: [Ready])
            // Also in ready, not only startup: a reachable database with no schema
            // cannot serve traffic, and compose watches /health/ready.
            .AddCheck<MigrationsHealthCheck>("migrations", tags: [Ready, Startup], timeout: TimeSpan.FromSeconds(1));

        // AddDbContextCheck takes no timeout, and the compose healthcheck gives the
        // whole ready probe 3s. Checks run in parallel, so 2s leaves margin.
        services.Configure<HealthCheckServiceOptions>(options =>
        {
            foreach (var registration in options.Registrations.Where(registration => registration.Name == Postgres))
            {
                registration.Timeout = TimeSpan.FromSeconds(2);
            }
        });

        return services;
    }

    public static void MapHealthCheckEndpoints(this IEndpointRouteBuilder routes)
    {
        // Liveness deliberately probes nothing: if it failed while Postgres was
        // down, the orchestrator would restart a process that has nothing wrong
        // with it, over and over, without fixing the actual problem.
        routes.MapHealthChecks("/health/live", OptionsFor(Live));
        routes.MapHealthChecks("/health/ready", OptionsFor(Ready));
        routes.MapHealthChecks("/health/startup", OptionsFor(Startup));
    }

    private static HealthCheckOptions OptionsFor(string tag) => new()
    {
        Predicate = registration => registration.Tags.Contains(tag),
        ResponseWriter = WriteReport,
    };

    private static Task WriteReport(HttpContext context, HealthReport report) =>
        context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            totalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
            }),
        });
}
