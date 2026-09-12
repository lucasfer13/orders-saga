namespace Payments.Api.Features.RefundPayment;

/// <summary>Refunded is false when there was no charge to refund, which is not an error: refunding twice must be safe.</summary>
internal sealed record RefundPaymentResult(bool Refunded);
