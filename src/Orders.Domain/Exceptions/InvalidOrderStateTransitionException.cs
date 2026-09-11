namespace Orders.Domain.Exceptions;

/// <summary>Se intentó aplicar una transición de estado que no es válida desde el estado actual del pedido.</summary>
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
