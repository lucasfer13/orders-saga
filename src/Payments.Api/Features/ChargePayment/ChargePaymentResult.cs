namespace Payments.Api.Features.ChargePayment;

/// <summary>A decline is an expected business outcome the saga must react to, not an exception.</summary>
internal sealed record ChargePaymentResult(bool Succeeded, Guid? ChargeId, string? DeclineReason)
{
    public static ChargePaymentResult Charged(Guid chargeId) => new(true, chargeId, null);

    public static ChargePaymentResult Declined(string reason) => new(false, null, reason);
}
