using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Shipping.Api.Persistence;

namespace Shipping.Api.Features.GetShipments;

internal static class GetShipmentsEndpoint
{
    public static void MapGetShipments(this IEndpointRouteBuilder routes) => routes.MapGet("/shipments", GetShipments);

    /// <summary>Shipments created so far, with their cancellation state.</summary>
    internal static async Task<Ok<IReadOnlyCollection<Shipment>>> GetShipments(
        ShippingDbContext context,
        CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyCollection<Shipment>>(
            await context.Shipments.AsNoTracking().ToListAsync(cancellationToken));
}
