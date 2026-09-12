using Microsoft.AspNetCore.Http.HttpResults;
using Shipping.Api.Storage;

namespace Shipping.Api.Features.GetShipments;

internal static class GetShipmentsEndpoint
{
    public static void MapGetShipments(this IEndpointRouteBuilder routes) => routes.MapGet("/shipments", GetShipments);

    /// <summary>Shipments created so far, with their cancellation state.</summary>
    internal static Ok<IReadOnlyCollection<Shipment>> GetShipments(ShipmentStore store) => TypedResults.Ok(store.Snapshot());
}
