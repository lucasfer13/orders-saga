namespace Orders.Domain.Events;

/// <summary>Something that actually happened to the Order aggregate. Publishing (outbox) is infrastructure's job — see T15.</summary>
public interface IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; }
}
