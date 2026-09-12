namespace Orders.Domain.Events;

/// <summary>Something that actually happened to the Order aggregate. Publishing it (via outbox) is infrastructure's job, not the domain's.</summary>
public interface IDomainEvent
{
    public DateTimeOffset OccurredAtUtc { get; }
}
