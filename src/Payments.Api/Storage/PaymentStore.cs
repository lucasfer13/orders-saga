namespace Payments.Api.Storage;

/// <summary>In-memory charges per order. Replaced by EF Core + PostgreSQL later.</summary>
internal sealed class PaymentStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, Charge> _chargesByOrder = [];

    public Guid Charge(Guid orderId, decimal amount, string currency)
    {
        lock (_gate)
        {
            var charge = new Charge(Guid.NewGuid(), orderId, amount, currency, Refunded: false);
            _chargesByOrder[orderId] = charge;
            return charge.Id;
        }
    }

    public Charge? FindCharge(Guid orderId)
    {
        lock (_gate)
        {
            return _chargesByOrder.GetValueOrDefault(orderId);
        }
    }

    public bool Refund(Guid orderId)
    {
        lock (_gate)
        {
            if (!_chargesByOrder.TryGetValue(orderId, out var charge) || charge.Refunded)
                return false;

            _chargesByOrder[orderId] = charge with { Refunded = true };
            return true;
        }
    }

    public IReadOnlyCollection<Charge> Snapshot()
    {
        lock (_gate)
        {
            return [.. _chargesByOrder.Values];
        }
    }
}

internal sealed record Charge(Guid Id, Guid OrderId, decimal Amount, string Currency, bool Refunded);
