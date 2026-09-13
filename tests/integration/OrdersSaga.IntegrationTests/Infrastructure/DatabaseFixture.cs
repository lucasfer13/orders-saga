using Npgsql;
using Testcontainers.PostgreSql;

namespace OrdersSaga.IntegrationTests.Infrastructure;

/// <summary>
/// One PostgreSQL container for the whole test assembly, holding one logical
/// database per service — the same topology the compose file uses.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupBudget = TimeSpan.FromSeconds(60);

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .Build();

    private ServiceFactory<Shipping.Api.ApiMarker>? _shipping;

    public ServiceFactory<Shipping.Api.ApiMarker> Shipping =>
        _shipping ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _shipping = new ServiceFactory<Shipping.Api.ApiMarker>(ConnectionStringFor("shipping"));

        await WaitUntilStarted(_shipping.CreateClient());
    }

    public async ValueTask DisposeAsync()
    {
        if (_shipping is not null)
        {
            await _shipping.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }

    /// <summary>
    /// Waits on the service's own startup probe instead of migrating from the test:
    /// a second way of creating the schema could drift from the real one.
    /// </summary>
    private static async Task WaitUntilStarted(HttpClient client)
    {
        using (client)
        {
            var deadline = DateTimeOffset.UtcNow + StartupBudget;
            var lastReport = "(no answer yet)";

            while (DateTimeOffset.UtcNow < deadline)
            {
                var response = await client.GetAsync(new Uri("/health/startup", UriKind.Relative));

                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                lastReport = await response.Content.ReadAsStringAsync();
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }

            throw new InvalidOperationException($"The service did not finish starting up: {lastReport}");
        }
    }

    private string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString;
}
