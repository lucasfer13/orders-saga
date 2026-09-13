using Inventory.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Features.ReserveStock;

internal sealed class ReserveStockHandler(InventoryDbContext context)
{
    public async Task<ReserveStockResult> HandleAsync(ReserveStockCommand command, CancellationToken cancellationToken)
    {
        // Deterministic order: two reservations competing for the same products must
        // not be able to wait on each other's row locks.
        var lines = command.Lines.OrderBy(line => line.ProductId).ToList();

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        foreach (var line in lines)
        {
            // Postgres evaluates the condition under the row lock the UPDATE itself takes,
            // so checking and discounting are a single atomic operation. The number of
            // affected rows is the answer; nothing is read back to decide.
            var discounted = await context.StockItems
                .Where(item => item.ProductId == line.ProductId && item.Available >= line.Quantity)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.Available, item => item.Available - line.Quantity),
                    cancellationToken);

            if (discounted == 0)
            {
                // All or nothing is the rollback's job, not a first pass of checks:
                // checking everything up front reopens the gap between check and write.
                await transaction.RollbackAsync(cancellationToken);
                return ReserveStockResult.Failure($"Not enough stock for product {line.ProductId}.");
            }

            context.StockReservations.Add(new StockReservation(command.OrderId, line.ProductId, line.Quantity));
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ReserveStockResult.Success();
    }
}
