namespace Inventory.Api.Storage;

/// <summary>In-memory stock and reservations. Replaced by EF Core + PostgreSQL later.</summary>
internal sealed class StockStore
{
    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, int> _availableByProduct = [];
    private readonly Dictionary<Guid, Dictionary<Guid, int>> _reservationsByOrder = [];

    public void SetStock(Guid productId, int quantity)
    {
        lock (_gate)
        {
            _availableByProduct[productId] = quantity;
        }
    }

    public int GetAvailable(Guid productId)
    {
        lock (_gate)
        {
            return _availableByProduct.GetValueOrDefault(productId);
        }
    }

    public IReadOnlyCollection<StockLevel> Snapshot()
    {
        lock (_gate)
        {
            return [.. _availableByProduct.Select(entry => new StockLevel(entry.Key, entry.Value))];
        }
    }

    /// <summary>
    /// Reserves every requested quantity or nothing at all: a partially reserved order
    /// would leave the saga compensating against a state it never actually reached.
    /// Returns the first product without enough stock, or null when the reservation succeeded.
    /// </summary>
    public Guid? Reserve(Guid orderId, IReadOnlyCollection<KeyValuePair<Guid, int>> quantitiesByProduct)
    {
        lock (_gate)
        {
            foreach (var (productId, quantity) in quantitiesByProduct)
            {
                if (_availableByProduct.GetValueOrDefault(productId) < quantity)
                    return productId;
            }

            foreach (var (productId, quantity) in quantitiesByProduct)
            {
                _availableByProduct[productId] -= quantity;
            }

            _reservationsByOrder[orderId] = quantitiesByProduct.ToDictionary(entry => entry.Key, entry => entry.Value);
            return null;
        }
    }

    public bool Release(Guid orderId)
    {
        lock (_gate)
        {
            if (!_reservationsByOrder.Remove(orderId, out var reserved))
                return false;

            foreach (var (productId, quantity) in reserved)
            {
                _availableByProduct[productId] = _availableByProduct.GetValueOrDefault(productId) + quantity;
            }

            return true;
        }
    }
}

internal sealed record StockLevel(Guid ProductId, int Available);
