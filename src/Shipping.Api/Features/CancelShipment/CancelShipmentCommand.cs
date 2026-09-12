namespace Shipping.Api.Features.CancelShipment;

internal sealed record CancelShipmentCommand(Guid OrderId);
