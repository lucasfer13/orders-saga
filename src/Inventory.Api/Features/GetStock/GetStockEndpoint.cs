using Inventory.Api.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Api.Features.GetStock;

internal static class GetStockEndpoint
{
    public static void MapGetStock(this IEndpointRouteBuilder routes) => routes.MapGet("/stock", GetStock);

    /// <summary>Current stock level per product.</summary>
    internal static async Task<Ok<IReadOnlyCollection<StockItem>>> GetStock(
        InventoryDbContext context,
        CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyCollection<StockItem>>(
            await context.StockItems.AsNoTracking().ToListAsync(cancellationToken));
}
