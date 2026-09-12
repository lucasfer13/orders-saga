using Inventory.Api.Storage;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Inventory.Api.Features.GetStock;

internal static class GetStockEndpoint
{
    public static void MapGetStock(this IEndpointRouteBuilder routes) => routes.MapGet("/stock", GetStock);

    /// <summary>Current stock level per product.</summary>
    internal static Ok<IReadOnlyCollection<StockLevel>> GetStock(StockStore store) => TypedResults.Ok(store.Snapshot());
}
