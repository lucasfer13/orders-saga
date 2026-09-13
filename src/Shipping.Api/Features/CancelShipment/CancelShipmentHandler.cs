using Microsoft.EntityFrameworkCore;
using Shipping.Api.Persistence;

namespace Shipping.Api.Features.CancelShipment;

internal sealed class CancelShipmentHandler(ShippingDbContext context)
{
    public async Task<CancelShipmentResult> HandleAsync(CancelShipmentCommand command, CancellationToken cancellationToken)
    {
        var shipment = await context.Shipments
            .SingleOrDefaultAsync(shipment => shipment.OrderId == command.OrderId, cancellationToken);

        if (shipment is null || !shipment.Cancel())
        {
            return new CancelShipmentResult(Cancelled: false);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new CancelShipmentResult(Cancelled: true);
    }
}
