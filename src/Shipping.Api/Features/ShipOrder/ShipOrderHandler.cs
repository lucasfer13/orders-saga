using Shipping.Api.Storage;

namespace Shipping.Api.Features.ShipOrder;

internal sealed class ShipOrderHandler(ShipmentStore store)
{
    public ShipOrderResult Handle(ShipOrderCommand command)
    {
        var trackingNumber = $"TRK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        store.Ship(command.OrderId, trackingNumber);
        return new ShipOrderResult(trackingNumber);
    }
}
