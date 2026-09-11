namespace Orders.Domain.Events;

public sealed record OrderPlaced(OrderId OrderId, CustomerId CustomerId, Money Total, DateTimeOffset OccurredAtUtc)
    : IDomainEvent;

public sealed record OrderStockReserved(OrderId OrderId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record OrderPaymentCharged(OrderId OrderId, Money Amount, DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record OrderShipped(OrderId OrderId, DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record OrderCompensationStarted(
    OrderId OrderId,
    OrderStatus FailedAtStatus,
    string Reason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent;

public sealed record OrderCancelled(OrderId OrderId, string Reason, DateTimeOffset OccurredAtUtc) : IDomainEvent;
