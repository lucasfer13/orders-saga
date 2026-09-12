using Microsoft.AspNetCore.Http.HttpResults;
using Payments.Api.Storage;

namespace Payments.Api.Features.GetPayments;

internal static class GetPaymentsEndpoint
{
    public static void MapGetPayments(this IEndpointRouteBuilder routes) => routes.MapGet("/payments", GetPayments);

    /// <summary>Charges recorded so far, with their refund state.</summary>
    internal static Ok<IReadOnlyCollection<Charge>> GetPayments(PaymentStore store) => TypedResults.Ok(store.Snapshot());
}
