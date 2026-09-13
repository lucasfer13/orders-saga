namespace Orders.Domain;

/// <summary>
/// Port for loading and storing the <see cref="Order"/> aggregate (ADR-0004). Orders.Api
/// implements it over its DbContext, and that implementation is the only type of the
/// service allowed to see it. No SaveChangesAsync and no transaction here: committing is
/// the pipeline's job, once Orders has one (T12).
/// </summary>
public interface IOrderRepository
{
    public Task AddAsync(Order order, CancellationToken cancellationToken);

    public Task<Order?> GetAsync(OrderId id, CancellationToken cancellationToken);
}
