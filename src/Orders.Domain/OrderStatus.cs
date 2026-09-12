namespace Orders.Domain;

/// <summary>Order aggregate status. Deliberately simpler than the saga's state machine (T18) — this only tracks what actually happened to the order.</summary>
public enum OrderStatus
{
    Pending,
    StockReserved,
    PaymentCharged,
    Shipped,
    Compensating,
    Cancelled,
}
