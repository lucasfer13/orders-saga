using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Payments.Api.Persistence;

namespace Payments.Api.Features.ChargePayment;

internal sealed class ChargePaymentHandler(PaymentsDbContext context, IOptions<PaymentsOptions> options)
{
    public async Task<ChargePaymentResult> HandleAsync(ChargePaymentCommand command, CancellationToken cancellationToken)
    {
        var declineAbove = options.Value.DeclineAboveAmount;

        // Checked before anything is written, so a declined charge leaves no row behind.
        if (declineAbove is not null && command.Amount > declineAbove)
        {
            return ChargePaymentResult.Declined(
                $"Amount {command.Amount} {command.Currency} is above the decline threshold of {declineAbove}.");
        }

        var charge = await context.Charges
            .SingleOrDefaultAsync(charge => charge.OrderId == command.OrderId, cancellationToken);

        if (charge is null)
        {
            charge = new Charge(command.OrderId, command.Amount, command.Currency);
            context.Charges.Add(charge);
        }
        else
        {
            charge.Recharge(command.Amount, command.Currency);
        }

        await context.SaveChangesAsync(cancellationToken);

        return ChargePaymentResult.Charged(charge.Id);
    }
}
