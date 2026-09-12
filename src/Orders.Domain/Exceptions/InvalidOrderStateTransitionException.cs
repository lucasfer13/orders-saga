namespace Orders.Domain.Exceptions;

/// <summary>Attempted a transition that's not valid from the order's current state.</summary>
public sealed class InvalidOrderStateTransitionException : OrderDomainException
{
    public InvalidOrderStateTransitionException(OrderStatus currentStatus, string attemptedTransition)
        : base($"No se puede aplicar '{attemptedTransition}' a un pedido en estado {currentStatus}.")
    {
        CurrentStatus = currentStatus;
        AttemptedTransition = attemptedTransition;
    }

    public OrderStatus CurrentStatus { get; }

    public string AttemptedTransition { get; }
}
