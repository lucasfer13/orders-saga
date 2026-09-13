namespace Inventory.Api.Persistence;

/// <summary>How many units of a product are free to be reserved.</summary>
internal sealed class StockItem
{
    private StockItem()
    {
        // Private constructor for EF Core materialization.
    }

    public StockItem(Guid productId, int available)
    {
        ProductId = productId;
        Available = available;
    }

    public Guid ProductId { get; private set; }

    /// <summary>
    /// Never changed through the change tracker: reserving and releasing go through a
    /// conditional UPDATE so the check and the discount are one atomic operation.
    /// </summary>
    public int Available { get; private set; }
}
