using Inventory.Api.Features.ReleaseStock;
using Inventory.Api.Features.ReserveStock;
using Inventory.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrdersSaga.IntegrationTests.Infrastructure;
using Shouldly;

namespace OrdersSaga.IntegrationTests.Persistence;

[Collection(SharedDatabase.Name)]
public class InventoryPersistenceTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Reserving_discounts_the_available_stock()
    {
        var product = await GivenStock(10);

        var result = await Reserve(Guid.NewGuid(), (product, 3));

        result.Succeeded.ShouldBeTrue();
        (await Available(product)).ShouldBe(7);
    }

    [Fact]
    public async Task Reserving_more_than_there_is_fails_and_changes_nothing()
    {
        var product = await GivenStock(2);

        var result = await Reserve(Guid.NewGuid(), (product, 3));

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNullOrWhiteSpace();
        (await Available(product)).ShouldBe(2);
    }

    [Fact]
    public async Task Reserving_an_unknown_product_fails()
    {
        var result = await Reserve(Guid.NewGuid(), (Guid.NewGuid(), 1));

        result.Succeeded.ShouldBeFalse();
    }

    /// <summary>All or nothing: a partly reserved order would leave the saga compensating a state that never happened.</summary>
    [Fact]
    public async Task Reserving_leaves_no_trace_when_one_line_does_not_fit()
    {
        var plenty = await GivenStock(10);
        var scarce = await GivenStock(1);
        var orderId = Guid.NewGuid();

        var result = await Reserve(orderId, (plenty, 5), (scarce, 5));

        result.Succeeded.ShouldBeFalse();
        (await Available(plenty)).ShouldBe(10);
        (await Available(scarce)).ShouldBe(1);
        (await ReservationCount(orderId)).ShouldBe(0);
    }

    /// <summary>
    /// The test the conditional update exists for: a read-check-write implementation
    /// passes every sequential test and oversells the moment two requests overlap.
    /// </summary>
    [Fact]
    public async Task Concurrent_reservations_never_oversell()
    {
        const int Fits = 6;
        const int Attempts = 20;
        const int PerOrder = 2;

        var product = await GivenStock(Fits * PerOrder);

        var results = await Task.WhenAll(Enumerable.Range(0, Attempts)
            .Select(_ => Task.Run(() => Reserve(Guid.NewGuid(), (product, PerOrder)))));

        results.Count(result => result.Succeeded).ShouldBe(Fits);
        (await Available(product)).ShouldBe(0);
    }

    /// <summary>
    /// The in-memory StockStore treated a second reservation for the same order and
    /// product as an overwrite, which leaked stock (the first reservation's units were
    /// never given back). The composite primary key on stock_reservations refuses the
    /// second insert outright and the transaction rolls back, so the failed attempt
    /// leaves the first reservation and the discounted stock untouched.
    /// </summary>
    [Fact]
    public async Task Reserving_the_same_order_and_product_twice_is_rejected_by_the_primary_key()
    {
        var product = await GivenStock(10);
        var orderId = Guid.NewGuid();

        (await Reserve(orderId, (product, 3))).Succeeded.ShouldBeTrue();

        await Should.ThrowAsync<DbUpdateException>(() => Reserve(orderId, (product, 2)));

        (await Available(product)).ShouldBe(7);
        (await ReservationCount(orderId)).ShouldBe(1);
    }

    /// <summary>
    /// ADR-0005 processes a reservation's lines in a deterministic order (by product id)
    /// precisely so that two reservations racing for the same two products cannot each
    /// hold one product's lock while waiting for the other's. Submitting the lines in
    /// opposite order per task is what would make a naive implementation deadlock.
    /// </summary>
    [Fact]
    public async Task Concurrent_reservations_across_two_products_do_not_deadlock()
    {
        const int Attempts = 24;

        var first = await GivenStock(Attempts);
        var second = await GivenStock(Attempts);

        var results = await Task.WhenAll(Enumerable.Range(0, Attempts).Select(attempt => Task.Run(() =>
            attempt % 2 == 0
                ? Reserve(Guid.NewGuid(), (first, 1), (second, 1))
                : Reserve(Guid.NewGuid(), (second, 1), (first, 1)))));

        results.ShouldAllBe(result => result.Succeeded);
        (await Available(first)).ShouldBe(0);
        (await Available(second)).ShouldBe(0);
    }

    [Fact]
    public async Task Releasing_gives_the_quantities_back_and_drops_the_reservations()
    {
        var first = await GivenStock(10);
        var second = await GivenStock(10);
        var orderId = Guid.NewGuid();

        await Reserve(orderId, (first, 4), (second, 6));

        var release = await Release(orderId);

        release.Released.ShouldBeTrue();
        (await Available(first)).ShouldBe(10);
        (await Available(second)).ShouldBe(10);
        (await ReservationCount(orderId)).ShouldBe(0);
    }

    [Fact]
    public async Task Releasing_an_order_without_reservations_reports_nothing_to_release()
    {
        (await Release(Guid.NewGuid())).Released.ShouldBeFalse();
    }

    [Fact]
    public async Task The_seeder_leaves_demo_stock_behind()
    {
        await using var scope = fixture.Inventory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        foreach (var seeded in StockSeedData.Items)
        {
            var stored = await context.StockItems
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.ProductId == seeded.ProductId, TestContext.Current.CancellationToken);

            stored.ShouldNotBeNull();
        }
    }

    private async Task<Guid> GivenStock(int available)
    {
        var productId = Guid.NewGuid();

        await using var scope = fixture.Inventory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        context.StockItems.Add(new StockItem(productId, available));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return productId;
    }

    private async Task<ReserveStockResult> Reserve(Guid orderId, params (Guid ProductId, int Quantity)[] lines)
    {
        await using var scope = fixture.Inventory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ReserveStockHandler>();

        return await handler.HandleAsync(
            new ReserveStockCommand(orderId, [.. lines.Select(line => new ReserveStockLine(line.ProductId, line.Quantity))]),
            TestContext.Current.CancellationToken);
    }

    private async Task<ReleaseStockResult> Release(Guid orderId)
    {
        await using var scope = fixture.Inventory.Services.CreateAsyncScope();
        var handler = scope.ServiceProvider.GetRequiredService<ReleaseStockHandler>();

        return await handler.HandleAsync(new ReleaseStockCommand(orderId), TestContext.Current.CancellationToken);
    }

    private async Task<int> Available(Guid productId)
    {
        await using var scope = fixture.Inventory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        return await context.StockItems
            .AsNoTracking()
            .Where(item => item.ProductId == productId)
            .Select(item => item.Available)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> ReservationCount(Guid orderId)
    {
        await using var scope = fixture.Inventory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();

        return await context.StockReservations
            .AsNoTracking()
            .CountAsync(reservation => reservation.OrderId == orderId, TestContext.Current.CancellationToken);
    }
}
