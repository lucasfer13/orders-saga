using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Persistence;
using Orders.Domain;
using OrdersSaga.IntegrationTests.Infrastructure;
using Shouldly;

namespace OrdersSaga.IntegrationTests.Persistence;

/// <summary>
/// Covers the header of the aggregate: id, customer, status and Total (a Money at the
/// root, which does map cleanly as a complex property). Lines is NOT covered here and
/// is not yet mapped at all — OwnedNavigationBuilder exposes no ComplexProperty, so
/// Money nested inside the owned Lines collection (UnitPrice) has no working path in
/// EF Core 10.0.12 yet, see the implementation report for what was tried. This test
/// does not claim to be the full round-trip docs/plan.md describes in step 4.1.
/// </summary>
[Collection(SharedDatabase.Name)]
public class OrdersPersistenceTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task An_order_s_header_survives_the_scope_it_was_created_in()
    {
        var customerId = CustomerId.New();
        var line = new OrderLine(ProductId.New(), 2, new Money(10m, "EUR"));
        var order = Order.Place(OrderId.New(), customerId, [line]);

        // The only explicit save in T11: the repository does not expose SaveChangesAsync,
        // committing is the pipeline's job once Orders has one (ADR-0004, T12).
        await using (var scope = fixture.Orders.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            await repository.AddAsync(order, TestContext.Current.CancellationToken);
            await scope.ServiceProvider.GetRequiredService<OrdersDbContext>()
                .SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Order? reloaded;
        await using (var scope = fixture.Orders.Services.CreateAsyncScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            reloaded = await repository.GetAsync(order.Id, TestContext.Current.CancellationToken);
        }

        reloaded.ShouldNotBeNull();
        reloaded.Id.ShouldBe(order.Id);
        reloaded.CustomerId.ShouldBe(customerId);
        reloaded.Status.ShouldBe(OrderStatus.Pending);
        reloaded.Total.ShouldBe(order.Total);
    }

    [Fact]
    public async Task An_unknown_order_is_not_found()
    {
        await using var scope = fixture.Orders.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        (await repository.GetAsync(OrderId.New(), TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
