namespace Inventory.Api;

internal sealed class InventoryOptions
{
    public const string SectionName = "Inventory";

    /// <summary>
    /// Whether to seed the demo stock after migrating. Off by default: seeding is a
    /// convenience for the demo and for the tests, not part of what the service does.
    /// </summary>
    public bool SeedStock { get; set; }
}
