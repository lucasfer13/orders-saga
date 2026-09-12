using Microsoft.Extensions.Options;
using Payments.Api.Storage;

namespace Payments.Api.Features.ChargePayment;

internal sealed class ChargePaymentHandler(PaymentStore store, IOptions<PaymentsOptions> options)
{
    public ChargePaymentResult Handle(ChargePaymentCommand command)
    {
        var declineAbove = options.Value.DeclineAboveAmount;

        if (declineAbove is not null && command.Amount > declineAbove)
        {
            return ChargePaymentResult.Declined(
                $"Amount {command.Amount} {command.Currency} is above the decline threshold of {declineAbove}.");
        }

        return ChargePaymentResult.Charged(store.Charge(command.OrderId, command.Amount, command.Currency));
    }
}
