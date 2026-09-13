using Inventory.Api.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Features.ReleaseStock;

internal sealed class ReleaseStockHandler(InventoryDbContext context)
{
    public async Task<ReleaseStockResult> HandleAsync(ReleaseStockCommand command, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var reservations = await context.StockReservations
            .Where(reservation => reservation.OrderId == command.OrderId)
            .OrderBy(reservation => reservation.ProductId)
            .ToListAsync(cancellationToken);

        if (reservations.Count == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new ReleaseStockResult(Released: false);
        }

        foreach (var reservation in reservations)
        {
            // Giving stock back has no condition to check, but it still goes through the
            // database so two releases cannot lose each other's increment.
            await context.StockItems
                .Where(item => item.ProductId == reservation.ProductId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.Available, item => item.Available + reservation.Quantity),
                    cancellationToken);
        }

        context.StockReservations.RemoveRange(reservations);

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReleaseStockResult(Released: true);
    }
}
