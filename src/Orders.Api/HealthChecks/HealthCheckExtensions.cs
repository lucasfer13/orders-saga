using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Orders.Api.HealthChecks;

internal static class HealthCheckExtensions
{
    private const string Live = "live";
    private const string Ready = "ready";
    private const string Startup = "startup";

    public static IServiceCollection AddServiceHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), tags: [Live], timeout: TimeSpan.FromSeconds(1));

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
