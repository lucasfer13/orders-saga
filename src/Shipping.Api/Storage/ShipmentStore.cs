namespace Shipping.Api.Storage;

/// <summary>In-memory shipments per order. Replaced by EF Core + PostgreSQL later.</summary>
internal sealed class ShipmentStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Shipment> _shipmentsByOrder = [];

    public void Ship(Guid orderId, string trackingNumber)
    {
        lock (_gate)
        {
            _shipmentsByOrder[orderId] = new Shipment(orderId, trackingNumber, Cancelled: false);
        }
    }

    public Shipment? FindShipment(Guid orderId)
    {
        lock (_gate)
        {
            return _shipmentsByOrder.GetValueOrDefault(orderId);
        }
    }

    public bool Cancel(Guid orderId)
    {
        lock (_gate)
        {
            if (!_shipmentsByOrder.TryGetValue(orderId, out var shipment) || shipment.Cancelled)
                return false;

            _shipmentsByOrder[orderId] = shipment with { Cancelled = true };
            return true;
        }
    }

    public IReadOnlyCollection<Shipment> Snapshot()
    {
        lock (_gate)
        {
            return [.. _shipmentsByOrder.Values];
        }
    }
}

internal sealed record Shipment(Guid OrderId, string TrackingNumber, bool Cancelled);
