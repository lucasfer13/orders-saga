using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shipping.Api.Persistence;

namespace Shipping.Api.HealthChecks;

internal sealed class MigrationsHealthCheck(MigrationState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var (status, error) = state.Read();

        // Unhealthy rather than Degraded while the migration is still running:
        // Degraded answers HTTP 200 and compose would read that as ready.
        return Task.FromResult(status switch
        {
            MigrationStatus.Applied => HealthCheckResult.Healthy("Migrations applied."),
            MigrationStatus.Failed => HealthCheckResult.Unhealthy($"Migrations failed: {error}"),
            _ => HealthCheckResult.Unhealthy($"Migrations not applied yet ({status})."),
        });
    }
}
