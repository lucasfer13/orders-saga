using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Payments.Api.Persistence.Configurations;

internal sealed class ChargeConfiguration : IEntityTypeConfiguration<Charge>
{
    public void Configure(EntityTypeBuilder<Charge> builder)
    {
        builder.HasKey(charge => charge.Id);
        builder.Property(charge => charge.Id).ValueGeneratedNever();

        // One charge per order, as the in-memory store guaranteed by keying on it.
        builder.HasIndex(charge => charge.OrderId).IsUnique();

        // Declared on purpose: a decimal without precision becomes an unconstrained numeric.
        builder.Property(charge => charge.Amount).HasPrecision(18, 2);
        builder.Property(charge => charge.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
        builder.Property(charge => charge.Refunded).IsRequired();
    }
}
