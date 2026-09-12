namespace Payments.Api.Features.ChargePayment;

internal sealed record ChargePaymentCommand(Guid OrderId, decimal Amount, string Currency);
