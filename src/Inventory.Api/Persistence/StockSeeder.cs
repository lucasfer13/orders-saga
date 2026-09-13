using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Persistence;

/// <summary>Idempotent: only inserts the products that are not there yet, so restarts do not reset quantities.</summary>
internal sealed class StockSeeder(InventoryDbContext context)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var known = await context.StockItems
            .Select(item => item.ProductId)
            .ToListAsync(cancellationToken);

        var missing = StockSeedData.Items.Where(item => !known.Contains(item.ProductId)).ToList();

        if (missing.Count == 0)
        {
            return;
        }

        context.StockItems.AddRange(missing);
        await context.SaveChangesAsync(cancellationToken);
    }
}
