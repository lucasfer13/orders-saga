using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrdersSaga.IntegrationTests.Infrastructure;
using Shipping.Api.Features.CancelShipment;
using Shipping.Api.Features.ShipOrder;
using Shipping.Api.Persistence;
using Shouldly;

namespace OrdersSaga.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public class ShippingPersistenceTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Shipping_an_order_survives_the_scope_it_was_created_in()
    {
        var orderId = Guid.NewGuid();

        var result = await Handle<ShipOrderHandler, ShipOrderResult>(
            handler => handler.HandleAsync(new ShipOrderCommand(orderId), TestContext.Current.CancellationToken));

        result.TrackingNumber.ShouldNotBeNullOrWhiteSpace();

        var stored = await Read(orderId);
        stored.ShouldNotBeNull();
        stored.TrackingNumber.ShouldBe(result.TrackingNumber);
        stored.Cancelled.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancelling_a_shipment_is_recorded()
    {
        var orderId = Guid.NewGuid();

        await Handle<ShipOrderHandler, ShipOrderResult>(
            handler => handler.HandleAsync(new ShipOrderCommand(orderId), TestContext.Current.CancellationToken));

        var cancellation = await Handle<CancelShipmentHandler, CancelShipmentResult>(
            handler => handler.HandleAsync(new CancelShipmentCommand(orderId), TestContext.Current.CancellationToken));

        cancellation.Cancelled.ShouldBeTrue();
        (await Read(orderId))!.Cancelled.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancelling_twice_reports_nothing_to_cancel()
    {
        var orderId = Guid.NewGuid();

        await Handle<ShipOrderHandler, ShipOrderResult>(
            handler => handler.HandleAsync(new ShipOrderCommand(orderId), TestContext.Current.CancellationToken));
        await Handle<CancelShipmentHandler, CancelShipmentResult>(
            handler => handler.HandleAsync(new CancelShipmentCommand(orderId), TestContext.Current.CancellationToken));

        var second = await Handle<CancelShipmentHandler, CancelShipmentResult>(
            handler => handler.HandleAsync(new CancelShipmentCommand(orderId), TestContext.Current.CancellationToken));

        second.Cancelled.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancelling_an_unknown_order_reports_nothing_to_cancel()
    {
        var result = await Handle<CancelShipmentHandler, CancelShipmentResult>(
            handler => handler.HandleAsync(new CancelShipmentCommand(Guid.NewGuid()), TestContext.Current.CancellationToken));

        result.Cancelled.ShouldBeFalse();
    }

    /// <summary>The previous behaviour, kept on purpose: shipping again replaces the shipment.</summary>
    [Fact]
    public async Task Shipping_an_order_twice_replaces_the_tracking_number()
    {
        var orderId = Guid.NewGuid();

        var first = await Handle<ShipOrderHandler, ShipOrderResult>(
            handler => handler.HandleAsync(new ShipOrderCommand(orderId), TestContext.Current.CancellationToken));
        var second = await Handle<ShipOrderHandler, ShipOrderResult>(
            handler => handler.HandleAsync(new ShipOrderCommand(orderId), TestContext.Current.CancellationToken));

        second.TrackingNumber.ShouldNotBe(first.TrackingNumber);
        (await Read(orderId))!.TrackingNumber.ShouldBe(second.TrackingNumber);
    }

    private async Task<TResult> Handle<THandler, TResult>(Func<THandler, Task<TResult>> act)
        where THandler : notnull
    {
        await using var scope = fixture.Shipping.Services.CreateAsyncScope();
        return await act(scope.ServiceProvider.GetRequiredService<THandler>());
    }

    private async Task<Shipment?> Read(Guid orderId)
    {
        await using var scope = fixture.Shipping.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ShippingDbContext>();

        return await context.Shipments
            .AsNoTracking()
            .SingleOrDefaultAsync(shipment => shipment.OrderId == orderId, TestContext.Current.CancellationToken);
    }
}
