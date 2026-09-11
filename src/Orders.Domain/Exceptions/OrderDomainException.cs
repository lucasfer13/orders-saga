namespace Orders.Domain.Exceptions;

/// <summary>
/// Base type for every invariant violation raised by the Order aggregate.
/// Mapped centrally to a 409/422 ProblemDetails response by Orders.Api
/// (ver ESTANDAR-CALIDAD.md, sección 2), nunca capturada aquí.
/// </summary>
public abstract class OrderDomainException : Exception
{
    protected OrderDomainException(string message) : base(message)
    {
    }
}
