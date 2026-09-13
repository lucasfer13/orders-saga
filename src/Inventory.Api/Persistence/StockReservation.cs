namespace Inventory.Api.Persistence;

/// <summary>
/// What one order took from one product. Kept row by row because the compensation
/// has to know how much to give back per product, not just per order.
/// </summary>
internal sealed class StockReservation
{
    private StockReservation()
    {
        // Private constructor for EF Core materialization.
    }

    public StockReservation(Guid orderId, Guid productId, int quantity)
    {
        OrderId = orderId;
        ProductId = productId;
        Quantity = quantity;
    }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }
}
