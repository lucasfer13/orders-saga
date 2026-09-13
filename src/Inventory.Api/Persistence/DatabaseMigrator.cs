using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Inventory.Api.Persistence;

/// <summary>
/// Creates the database if it is not there yet, applies the pending migrations and,
/// when configuration asks for it, seeds the demo stock. Publishes its progress so
/// /health/startup can report it.
/// </summary>
internal sealed partial class DatabaseMigrator(
    IServiceScopeFactory scopeFactory,
    MigrationState state,
    IOptions<InventoryOptions> options,
    ILogger<DatabaseMigrator> logger) : BackgroundService
{
    private const int MaxAttempts = 10;
    private static readonly TimeSpan DelayBetweenAttempts = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield before touching anything: the host must be listening and answering
        // /health/live while the schema is still being built.
        await Task.Yield();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            state.MarkRunning();

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
                await context.Database.MigrateAsync(stoppingToken);

                if (options.Value.SeedStock)
                {
                    await scope.ServiceProvider.GetRequiredService<StockSeeder>().SeedAsync(stoppingToken);
                    LogSeeded(logger);
                }

                state.MarkApplied();
                LogApplied(logger);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Postgres can answer pg_isready a moment before it accepts connections,
                // and the four services start at once, so a first failure is expected.
                state.MarkFailed(exception.Message);
                LogAttemptFailed(logger, exception, attempt, MaxAttempts);

                if (attempt == MaxAttempts)
                {
                    // The process stays up on purpose: a restart loop would hide the reason,
                    // which is readable in the JSON of /health/startup.
                    return;
                }

                await Task.Delay(DelayBetweenAttempts, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database migrations applied.")]
    private static partial void LogApplied(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo stock seeded.")]
    private static partial void LogSeeded(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Migration attempt {Attempt} of {MaxAttempts} failed.")]
    private static partial void LogAttemptFailed(ILogger logger, Exception exception, int attempt, int maxAttempts);
}
