using Inventory.Api.Storage;

namespace Inventory.Api.Features.ReleaseStock;

internal sealed class ReleaseStockHandler(StockStore store)
{
    public ReleaseStockResult Handle(ReleaseStockCommand command) => new(store.Release(command.OrderId));
}
