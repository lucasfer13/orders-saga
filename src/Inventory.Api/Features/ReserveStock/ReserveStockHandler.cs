using Inventory.Api.Storage;

namespace Inventory.Api.Features.ReserveStock;

internal sealed class ReserveStockHandler(StockStore store)
{
    public ReserveStockResult Handle(ReserveStockCommand command)
    {
        var quantities = command.Lines
            .Select(line => new KeyValuePair<Guid, int>(line.ProductId, line.Quantity))
            .ToList();

        var unavailableProductId = store.Reserve(command.OrderId, quantities);

        return unavailableProductId is null
            ? ReserveStockResult.Success()
            : ReserveStockResult.Failure($"Not enough stock for product {unavailableProductId}.");
    }
}
