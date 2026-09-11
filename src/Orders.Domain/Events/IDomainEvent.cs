namespace Orders.Domain.Events;

/// <summary>
/// Algo que le ha pasado de verdad al agregado Order. Quién los publica de
/// verdad (vía outbox) es infraestructura, en Orders.Api — ver docs/BACKLOG.md, T15.
/// </summary>
public interface IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; }
}
