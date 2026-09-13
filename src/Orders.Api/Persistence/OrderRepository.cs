using Microsoft.EntityFrameworkCore;
using Orders.Domain;

namespace Orders.Api.Persistence;

/// <summary>
/// Implements the port Orders.Domain declares (ADR-0004). The only type of this
/// service allowed to see <see cref="OrdersDbContext"/> — no other type in
/// Orders.Api may depend on it. Deliberately does not call SaveChangesAsync or open
/// a transaction: committing is the pipeline's job, once Orders has one (T12). In
/// T11 the only caller that saves explicitly is the round-trip integration test.
/// </summary>
internal sealed class OrderRepository(OrdersDbContext context) : IOrderRepository
{
    public Task AddAsync(Order order, CancellationToken cancellationToken)
    {
        context.Orders.Add(order);
        return Task.CompletedTask;
    }

    public async Task<Order?> GetAsync(OrderId id, CancellationToken cancellationToken) =>
        // Owned collections load with their owner automatically: no Include needed,
        // and lazy loading stays off across the repo (see docs/plan.md).
        await context.Orders.SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
}
