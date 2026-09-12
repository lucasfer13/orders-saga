namespace Shipping.Api.Features.CancelShipment;

/// <summary>Cancelled is false when there was no shipment to cancel, which is not an error: cancelling twice must be safe.</summary>
internal sealed record CancelShipmentResult(bool Cancelled);
