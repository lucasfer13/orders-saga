namespace Inventory.Api.Features.ReleaseStock;

/// <summary>Released is false when there was nothing to release, which is not an error: releasing twice must be safe.</summary>
internal sealed record ReleaseStockResult(bool Released);
