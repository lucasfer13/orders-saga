namespace Shipping.Api.Persistence;

/// <summary>One shipment per order, with its tracking number and cancellation state.</summary>
internal sealed class Shipment
{
    private Shipment()
    {
        // Private constructor for EF Core materialization.
    }

    public Shipment(Guid orderId, string trackingNumber)
    {
        OrderId = orderId;
        TrackingNumber = trackingNumber;
    }

    public Guid OrderId { get; private set; }

    public string TrackingNumber { get; private set; } = null!;

    public bool Cancelled { get; private set; }

    /// <summary>Shipping an order that already had a shipment replaces it, as the in-memory store did.</summary>
    public void Reship(string trackingNumber)
    {
        TrackingNumber = trackingNumber;
        Cancelled = false;
    }

    /// <summary>Returns false when there was nothing to cancel, so cancelling twice stays safe.</summary>
    public bool Cancel()
    {
        if (Cancelled)
        {
            return false;
        }

        Cancelled = true;
        return true;
    }
}
