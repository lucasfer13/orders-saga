using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Payments.Api.Persistence;

namespace Payments.Api.Features.GetPayments;

internal static class GetPaymentsEndpoint
{
    public static void MapGetPayments(this IEndpointRouteBuilder routes) => routes.MapGet("/payments", GetPayments);

    /// <summary>Charges recorded so far, with their refund state.</summary>
    internal static async Task<Ok<IReadOnlyCollection<Charge>>> GetPayments(
        PaymentsDbContext context,
        CancellationToken cancellationToken) =>
        TypedResults.Ok<IReadOnlyCollection<Charge>>(
            await context.Charges.AsNoTracking().ToListAsync(cancellationToken));
}
