namespace Inventory.Api.Features.ReserveStock;

internal sealed record ReserveStockResult(bool Succeeded, string? FailureReason)
{
    public static ReserveStockResult Success() => new(true, null);

    public static ReserveStockResult Failure(string reason) => new(false, reason);
}
