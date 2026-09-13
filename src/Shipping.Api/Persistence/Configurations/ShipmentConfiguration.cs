using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Shipping.Api.Persistence.Configurations;

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        // The order owns the shipment, so the order identifier is the key.
        builder.HasKey(shipment => shipment.OrderId);
        builder.Property(shipment => shipment.OrderId).ValueGeneratedNever();
        builder.Property(shipment => shipment.TrackingNumber).IsRequired().HasMaxLength(32);
        builder.Property(shipment => shipment.Cancelled).IsRequired();
    }
}
