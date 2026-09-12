namespace Inventory.Api.Features.ReserveStock;

internal sealed record ReserveStockCommand(Guid OrderId, IReadOnlyCollection<ReserveStockLine> Lines);

internal sealed record ReserveStockLine(Guid ProductId, int Quantity);
