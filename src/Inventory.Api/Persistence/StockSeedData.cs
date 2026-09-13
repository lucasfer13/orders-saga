namespace Inventory.Api.Persistence;

/// <summary>
/// Demo stock, in code rather than in a migration so adjusting a quantity does not
/// mean generating a new migration. Which products and quantities is still open —
/// see docs/plan.md — so the whole list lives here, in one place, easy to replace.
/// Identifiers are fixed so a demo script can refer to them.
/// </summary>
internal static class StockSeedData
{
    public static IReadOnlyCollection<StockItem> Items { get; } =
    [
        new(Guid.Parse("11111111-1111-1111-1111-111111111111"), 100),
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), 50),
        new(Guid.Parse("33333333-3333-3333-3333-333333333333"), 10),
    ];
}
