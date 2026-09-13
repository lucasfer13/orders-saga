using Microsoft.EntityFrameworkCore;
using Shipping.Api.Persistence;

namespace Shipping.Api.Features.ShipOrder;

internal sealed class ShipOrderHandler(ShippingDbContext context)
{
    public async Task<ShipOrderResult> HandleAsync(ShipOrderCommand command, CancellationToken cancellationToken)
    {
        var trackingNumber = $"TRK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var shipment = await context.Shipments
            .SingleOrDefaultAsync(shipment => shipment.OrderId == command.OrderId, cancellationToken);

        if (shipment is null)
        {
            context.Shipments.Add(new Shipment(command.OrderId, trackingNumber));
        }
        else
        {
            shipment.Reship(trackingNumber);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new ShipOrderResult(trackingNumber);
    }
}
