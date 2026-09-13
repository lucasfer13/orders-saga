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

    // Money is a reference type (ADR-0007); EF sets this via the backing field, the
    // null-forgiving default only satisfies the compiler between construction and that.
    public Money UnitPrice { get; } = null!;

    public Money LineTotal => UnitPrice * Quantity;
}
