using Shipping.Api.Features.CancelShipment;
using Shipping.Api.Storage;
using Shouldly;

namespace OrdersSaga.UnitTests.ShippingApi;

public class CancelShipmentHandlerTests
{
    [Fact]
    public void Cancels_an_existing_shipment()
    {
        var orderId = Guid.NewGuid();
        var store = new ShipmentStore();
        store.Ship(orderId, "TRK-0001");

        var result = new CancelShipmentHandler(store).Handle(new CancelShipmentCommand(orderId));

        result.Cancelled.ShouldBeTrue();
        store.FindShipment(orderId)!.Cancelled.ShouldBeTrue();
    }

    [Fact]
    public void Does_nothing_when_the_order_has_no_shipment()
    {
        var store = new ShipmentStore();

        var result = new CancelShipmentHandler(store).Handle(new CancelShipmentCommand(Guid.NewGuid()));

        result.Cancelled.ShouldBeFalse();
    }
}
