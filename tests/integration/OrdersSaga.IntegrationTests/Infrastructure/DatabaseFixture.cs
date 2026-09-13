using System.Globalization;
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

    // Same image as docker-compose.yml, so the tests exercise the engine the demo runs on.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine").Build();

    /// <summary>Fixed here so the decline path is reproducible instead of depending on configuration.</summary>
    public const decimal PaymentDeclineThreshold = 100m;

    private ServiceFactory<Shipping.Api.ApiMarker>? _shipping;
    private ServiceFactory<Payments.Api.ApiMarker>? _payments;
    private ServiceFactory<Inventory.Api.ApiMarker>? _inventory;
    private ServiceFactory<Orders.Api.ApiMarker>? _orders;

    public ServiceFactory<Shipping.Api.ApiMarker> Shipping =>
        _shipping ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public ServiceFactory<Payments.Api.ApiMarker> Payments =>
        _payments ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public ServiceFactory<Inventory.Api.ApiMarker> Inventory =>
        _inventory ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public ServiceFactory<Orders.Api.ApiMarker> Orders =>
        _orders ?? throw new InvalidOperationException("The fixture has not been initialised.");

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        _shipping = new ServiceFactory<Shipping.Api.ApiMarker>(ConnectionStringFor("shipping"));
        _payments = new ServiceFactory<Payments.Api.ApiMarker>(
            ConnectionStringFor("payments"),
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Payments:DeclineAboveAmount"] = PaymentDeclineThreshold.ToString(CultureInfo.InvariantCulture),
            });
        _inventory = new ServiceFactory<Inventory.Api.ApiMarker>(
            ConnectionStringFor("inventory"),
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                // On here and in compose, off everywhere else: without stock there is no demo.
                ["Inventory:SeedStock"] = "true",
            });
        _orders = new ServiceFactory<Orders.Api.ApiMarker>(ConnectionStringFor("orders"));

        await WaitUntilStarted(_shipping.CreateClient());
        await WaitUntilStarted(_payments.CreateClient());
        await WaitUntilStarted(_inventory.CreateClient());
        await WaitUntilStarted(_orders.CreateClient());
    }

    public async ValueTask DisposeAsync()
    {
        if (_shipping is not null)
        {
            await _shipping.DisposeAsync();
        }

        if (_payments is not null)
        {
            await _payments.DisposeAsync();
        }

        if (_inventory is not null)
        {
            await _inventory.DisposeAsync();
        }

        if (_orders is not null)
        {
            await _orders.DisposeAsync();
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
