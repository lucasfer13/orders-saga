using Inventory.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Persistence;
using OrdersSaga.IntegrationTests.Infrastructure;
using Payments.Api.Persistence;
using Shipping.Api.Persistence;
using Shouldly;

namespace OrdersSaga.IntegrationTests.Persistence;

/// <summary>
/// Every service applies Database.Migrate() from a BackgroundService on every start
/// (ADR-0002), including a restart against a database that is already up to date. The
/// fixture already migrated each service once before any test in the assembly runs, so
/// applying migrations again here reproduces exactly that restart and has to stay a
/// no-op: no exception, and nothing left pending.
/// </summary>
[Collection(SharedDatabase.Name)]
public class MigrationsIdempotencyTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Shipping_migrations_apply_cleanly_a_second_time()
    {
        await using var scope = fixture.Shipping.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<ShippingDbContext>().Database;

        await Should.NotThrowAsync(() => database.MigrateAsync(TestContext.Current.CancellationToken));

        (await database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Payments_migrations_apply_cleanly_a_second_time()
    {
        await using var scope = fixture.Payments.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database;

        await Should.NotThrowAsync(() => database.MigrateAsync(TestContext.Current.CancellationToken));

        (await database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Inventory_migrations_apply_cleanly_a_second_time()
    {
        await using var scope = fixture.Inventory.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<InventoryDbContext>().Database;

        await Should.NotThrowAsync(() => database.MigrateAsync(TestContext.Current.CancellationToken));

        (await database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Orders_migrations_apply_cleanly_a_second_time()
    {
        await using var scope = fixture.Orders.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database;

        await Should.NotThrowAsync(() => database.MigrateAsync(TestContext.Current.CancellationToken));

        (await database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    /// <summary>
    /// The seeder is the other half of the migrator's idempotency: a restart must not
    /// duplicate or reset the demo stock (docs/plan.md, Paso 3). Counts only the seeded
    /// product ids rather than the whole table, since other tests in this shared
    /// database add their own stock items with unrelated ids.
    /// </summary>
    [Fact]
    public async Task Reseeding_inventory_does_not_duplicate_stock()
    {
        await using var scope = fixture.Inventory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        await scope.ServiceProvider.GetRequiredService<StockSeeder>().SeedAsync(TestContext.Current.CancellationToken);

        var seededIds = StockSeedData.Items.Select(item => item.ProductId).ToArray();
        (await context.StockItems.CountAsync(item => seededIds.Contains(item.ProductId), TestContext.Current.CancellationToken))
            .ShouldBe(StockSeedData.Items.Count);
    }
}
