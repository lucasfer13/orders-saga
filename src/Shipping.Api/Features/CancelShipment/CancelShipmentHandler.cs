using Shipping.Api.Storage;

namespace Shipping.Api.Features.CancelShipment;

internal sealed class CancelShipmentHandler(ShipmentStore store)
{
    public CancelShipmentResult Handle(CancelShipmentCommand command) => new(store.Cancel(command.OrderId));
}
