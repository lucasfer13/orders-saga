namespace Payments.Api.Features.RefundPayment;

internal sealed record RefundPaymentCommand(Guid OrderId);
