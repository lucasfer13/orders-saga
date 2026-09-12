using Shipping.Api.Features.ShipOrder;
using Shipping.Api.Storage;
using Shouldly;

namespace OrdersSaga.UnitTests.ShippingApi;

public class ShipOrderHandlerTests
{
    [Fact]
    public void Creates_a_shipment_with_a_tracking_number()
    {
        var orderId = Guid.NewGuid();
        var store = new ShipmentStore();

        var result = new ShipOrderHandler(store).Handle(new ShipOrderCommand(orderId));

        result.TrackingNumber.ShouldNotBeNullOrWhiteSpace();
        store.FindShipment(orderId).ShouldNotBeNull();
    }
}
