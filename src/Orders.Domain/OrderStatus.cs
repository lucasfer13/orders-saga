namespace Orders.Domain;

/// <summary>Order aggregate status, deliberately simpler than the saga's own state machine — this only tracks what actually happened to the order.</summary>
public enum OrderStatus
{
    Pending,
    StockReserved,
    PaymentCharged,
    Shipped,
    Compensating,
    Cancelled,
}
