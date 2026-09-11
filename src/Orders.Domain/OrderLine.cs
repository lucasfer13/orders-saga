namespace Orders.Domain;

public sealed record OrderLine
{
    public OrderLine(ProductId productId, int quantity, Money unitPrice)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "La cantidad debe ser mayor que cero.");

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public ProductId ProductId { get; }

    public int Quantity { get; }

    public Money UnitPrice { get; }

    public Money LineTotal => UnitPrice * Quantity;
}
