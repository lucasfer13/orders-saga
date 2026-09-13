using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Orders.Domain;

namespace Orders.Api.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(order => order.Id);
        builder.Property(order => order.Id).ValueGeneratedNever();

        builder.Property(order => order.CustomerId).IsRequired();
        builder.Property(order => order.Status).HasConversion<string>().IsRequired();

        // Not persistent state: a list of events accumulated to publish, see ADR-0003.
        builder.Ignore(order => order.DomainEvents);

        // Two consultable columns, not a single "12.50 EUR" column (ADR-0003). Money is
        // now a reference type, so the complex property itself needs an explicit
        // IsRequired() — EF no longer gets that for free the way it did from a struct
        // (ADR-0007).
        builder.ComplexProperty(order => order.Total, total =>
        {
            total.IsRequired();
            total.Property(money => money.Amount).HasColumnName("total_amount").HasPrecision(18, 2);
            total.Property(money => money.Currency).HasColumnName("total_currency").IsRequired().HasMaxLength(3).IsFixedLength();
        });

        // Own collection in its own table, mapped by the backing field _lines, with the
        // default shadow key (order + line index), not a business key (ADR-0003).
        builder.OwnsMany(order => order.Lines, lines =>
        {
            lines.ToTable("order_lines");
            lines.UsePropertyAccessMode(PropertyAccessMode.Field);

            lines.Property(line => line.ProductId).IsRequired();
            lines.Property(line => line.Quantity).IsRequired();

            // OwnsOne, not ComplexProperty: OwnedNavigationBuilder does not expose it. Only
            // reachable now that Money is a class, satisfying OwnsOne's `where
            // TNewRelatedEntity : class` constraint (ADR-0007). Unlike ComplexPropertyBuilder
            // above, OwnedNavigationBuilder exposes no IsRequired() for the owned reference
            // itself — EF infers it from the non-nullable Money CLR type instead.
            lines.OwnsOne(line => line.UnitPrice, unitPrice =>
            {
                unitPrice.Property(money => money.Amount).HasColumnName("unit_price_amount").HasPrecision(18, 2);
                unitPrice.Property(money => money.Currency).HasColumnName("unit_price_currency").IsRequired().HasMaxLength(3).IsFixedLength();
            });
        });
    }
}
