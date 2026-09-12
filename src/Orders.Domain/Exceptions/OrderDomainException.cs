namespace Orders.Domain.Exceptions;

/// <summary>Base type for every invariant violation raised by the Order aggregate. Mapped centrally to 409/422 by Orders.Api.</summary>
public abstract class OrderDomainException : Exception
{
    protected OrderDomainException(string message) : base(message)
    {
    }
}
