namespace Orders.Domain.Exceptions;

/// <summary>Un pedido se intentó construir en un estado que viola sus invariantes (p. ej. sin líneas).</summary>
public sealed class InvalidOrderException : OrderDomainException
{
    public InvalidOrderException(string message) : base(message)
    {
    }
}
