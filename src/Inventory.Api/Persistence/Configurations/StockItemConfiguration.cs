using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Inventory.Api.Persistence.Configurations;

internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> builder)
    {
        builder.HasKey(item => item.ProductId);
        builder.Property(item => item.ProductId).ValueGeneratedNever();
        builder.Property(item => item.Available).IsRequired();

        // A declarative safety net, not the real check: the conditional UPDATE in the
        // handler is what prevents overselling. If anyone ever writes the discount
        // without its condition, the database refuses instead of overselling quietly.
        builder.ToTable(table => table.HasCheckConstraint(
            "ck_stock_items_available_not_negative",
            "available >= 0"));
    }
}
