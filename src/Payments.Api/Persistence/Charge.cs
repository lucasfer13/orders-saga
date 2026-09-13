namespace Payments.Api.Persistence;

/// <summary>One charge per order, with its refund state.</summary>
internal sealed class Charge
{
    private Charge()
    {
        // Private constructor for EF Core materialization.
    }

    public Charge(Guid orderId, decimal amount, string currency)
    {
        Id = Guid.NewGuid();
        OrderId = orderId;
        Amount = amount;
        Currency = currency;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = null!;

    public bool Refunded { get; private set; }

    /// <summary>Charging an order that already had a charge replaces it, as the in-memory store did.</summary>
    public void Recharge(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
        Refunded = false;
    }

    /// <summary>Returns false when there was nothing to refund, so refunding twice stays safe.</summary>
    public bool Refund()
    {
        if (Refunded)
        {
            return false;
        }

        Refunded = true;
        return true;
    }
}
