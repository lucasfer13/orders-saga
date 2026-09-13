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

        // Two consultable columns, not a single "12.50 EUR" column (ADR-0003).
        builder.ComplexProperty(order => order.Total, total =>
        {
            total.Property(money => money.Amount).HasColumnName("total_amount").HasPrecision(18, 2);
            total.Property(money => money.Currency).HasColumnName("total_currency").IsRequired().HasMaxLength(3).IsFixedLength();
        });

        // Lines is deliberately NOT mapped yet. ADR-0006 authorised a private
        // parameterless constructor on OrderLine to unblock materialization, but that
        // only solves construction — OwnedNavigationBuilder (the OwnsMany builder)
        // exposes no ComplexProperty, so Money (UnitPrice) cannot be declared as a
        // nested complex type from the fluent API. The two ways around that were both
        // tried and both fail:
        //   - Reaching the underlying mutable metadata directly (IMutableTypeBase.
        //     AddComplexProperty) does let the model build and script a migration, but
        //     EF's own snapshot code generator then emits `b1.ComplexProperty(...)` for
        //     it in the checked-in Designer/ModelSnapshot files — a call that does not
        //     exist on OwnedNavigationBuilder either, so the generated code fails to
        //     compile. Not a workaround: a dead end.
        //   - OwnsMany(...).ToJson() (ADR-0003's candidate #1) now builds cleanly with
        //     the private constructor in place — untested at ADR-0006 time — but it
        //     serializes the whole collection into one jsonb column, which is exactly
        //     the SQL-queryability loss ADR-0003 rejected it for. Adopting it here would
        //     silently reopen an accepted ADR, which is not this implementer's call.
        // Left unmapped rather than shipped broken or silently reopening the ADR — see
        // the implementation report for the two remaining candidates.
        builder.Ignore(order => order.Lines);
    }
}
