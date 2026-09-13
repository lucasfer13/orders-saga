namespace Orders.Domain;

public sealed record OrderLine
{
    private OrderLine()
    {
        // Private constructor for EF Core materialization, mirroring Order (ADR-0006):
        // a complex property (Money) cannot be bound to a constructor parameter.
    }

    public OrderLine(ProductId productId, int quantity, Money unitPrice)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be greater than zero.");

        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public ProductId ProductId { get; }

    public int Quantity { get; }

    public Money UnitPrice { get; }

    public Money LineTotal => UnitPrice * Quantity;
}
