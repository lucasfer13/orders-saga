using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Persistence;
using Orders.Domain;
using OrdersSaga.IntegrationTests.Infrastructure;
using Shouldly;

namespace OrdersSaga.IntegrationTests.Persistence;

/// <summary>
/// Covers the full aggregate round trip across two scopes: id, customer, status, Total
/// and Lines with their own amounts and currencies, per docs/plan.md Paso 4.1.
/// </summary>
[Collection(SharedDatabase.Name)]
public class OrdersPersistenceTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task An_order_survives_the_scope_it_was_created_in_including_its_lines()
    {
        var customerId = CustomerId.New();
        var firstLine = new OrderLine(ProductId.New(), 2, new Money(10m, "EUR"));
        var secondLine = new OrderLine(ProductId.New(), 1, new Money(5m, "EUR"));
        var order = Order.Place(OrderId.New(), customerId, [firstLine, secondLine]);

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

        reloaded.Lines.OrderBy(line => line.ProductId.Value)
            .ShouldBe(order.Lines.OrderBy(line => line.ProductId.Value));
    }

    [Fact]
    public async Task An_unknown_order_is_not_found()
    {
        await using var scope = fixture.Orders.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        (await repository.GetAsync(OrderId.New(), TestContext.Current.CancellationToken)).ShouldBeNull();
    }
}
