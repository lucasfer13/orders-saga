using Orders.Domain.Events;
using Orders.Domain.Exceptions;

namespace Orders.Domain;

/// <summary>Coordinates the saga and has real domain invariants, unlike the thin Inventory/Payments/Shipping slices — see CLAUDE.md.</summary>
public sealed class Order
{
    private readonly List<OrderLine> _lines = [];
    private readonly List<IDomainEvent> _domainEvents = [];

    private Order()
    {
        // Private constructor for EF Core materialization.
    }

    private Order(OrderId id, CustomerId customerId, IReadOnlyCollection<OrderLine> lines)
    {
        Id = id;
        CustomerId = customerId;
        _lines.AddRange(lines);
        Total = lines.Aggregate(Money.Zero(), (total, line) => total + line.LineTotal);
        Status = OrderStatus.Pending;
    }

    public OrderId Id { get; private set; }

    public CustomerId CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    // Money is a reference type (ADR-0007); EF sets this via the backing field, the
    // null-forgiving default only satisfies the compiler between construction and that.
    public Money Total { get; private set; } = null!;

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    public static Order Place(OrderId id, CustomerId customerId, IReadOnlyCollection<OrderLine> lines)
    {
        if (lines.Count == 0)
            throw new InvalidOrderException("An order must have at least one line.");

        var order = new Order(id, customerId, lines);
        order.Raise(new OrderPlaced(order.Id, order.CustomerId, order.Total, DateTimeOffset.UtcNow));
        return order;
    }

    public void MarkStockReserved()
    {
        EnsureStatus(OrderStatus.Pending, nameof(MarkStockReserved));
        Status = OrderStatus.StockReserved;
        Raise(new OrderStockReserved(Id, DateTimeOffset.UtcNow));
    }

    public void MarkPaymentCharged()
    {
        EnsureStatus(OrderStatus.StockReserved, nameof(MarkPaymentCharged));
        Status = OrderStatus.PaymentCharged;
        Raise(new OrderPaymentCharged(Id, Total, DateTimeOffset.UtcNow));
    }

    public void MarkShipped()
    {
        EnsureStatus(OrderStatus.PaymentCharged, nameof(MarkShipped));
        Status = OrderStatus.Shipped;
        Raise(new OrderShipped(Id, DateTimeOffset.UtcNow));
    }

    public void BeginCompensation(string reason)
    {
        if (Status is not (OrderStatus.StockReserved or OrderStatus.PaymentCharged))
            throw new InvalidOrderStateTransitionException(Status, nameof(BeginCompensation));

        var failedAtStatus = Status;
        Status = OrderStatus.Compensating;
        Raise(new OrderCompensationStarted(Id, failedAtStatus, reason, DateTimeOffset.UtcNow));
    }

    public void Cancel(string reason)
    {
        EnsureStatus(OrderStatus.Compensating, nameof(Cancel));
        Status = OrderStatus.Cancelled;
        Raise(new OrderCancelled(Id, reason, DateTimeOffset.UtcNow));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void EnsureStatus(OrderStatus expected, string attemptedTransition)
    {
        if (Status != expected)
            throw new InvalidOrderStateTransitionException(Status, attemptedTransition);
    }

    private void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
