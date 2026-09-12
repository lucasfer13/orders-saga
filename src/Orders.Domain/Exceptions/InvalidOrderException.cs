namespace Orders.Domain.Exceptions;

/// <summary>An order was constructed in a state that violates its invariants (e.g. no lines).</summary>
public sealed class InvalidOrderException : OrderDomainException
{
    public InvalidOrderException(string message) : base(message)
    {
    }
}
