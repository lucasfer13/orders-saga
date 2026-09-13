using Microsoft.EntityFrameworkCore;
using Payments.Api.Persistence;

namespace Payments.Api.Features.RefundPayment;

internal sealed class RefundPaymentHandler(PaymentsDbContext context)
{
    public async Task<RefundPaymentResult> HandleAsync(RefundPaymentCommand command, CancellationToken cancellationToken)
    {
        var charge = await context.Charges
            .SingleOrDefaultAsync(charge => charge.OrderId == command.OrderId, cancellationToken);

        if (charge is null || !charge.Refund())
        {
            return new RefundPaymentResult(Refunded: false);
        }

        await context.SaveChangesAsync(cancellationToken);

        return new RefundPaymentResult(Refunded: true);
    }
}
