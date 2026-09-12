using Payments.Api.Storage;

namespace Payments.Api.Features.RefundPayment;

internal sealed class RefundPaymentHandler(PaymentStore store)
{
    public RefundPaymentResult Handle(RefundPaymentCommand command) => new(store.Refund(command.OrderId));
}
