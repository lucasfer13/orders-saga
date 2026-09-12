namespace Payments.Api;

internal sealed class PaymentsOptions
{
    public const string SectionName = "Payments";

    /// <summary>
    /// Charges above this amount are declined. Deterministic on purpose: the saga demo needs a
    /// reproducible payment failure, and randomness would make the compensation tests flaky.
    /// Null disables declining altogether.
    /// </summary>
    public decimal? DeclineAboveAmount { get; set; }
}
