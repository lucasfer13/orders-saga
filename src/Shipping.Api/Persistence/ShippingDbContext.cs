using Microsoft.EntityFrameworkCore;

namespace Shipping.Api.Persistence;

internal sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options) : DbContext(options)
{
    public DbSet<Shipment> Shipments => Set<Shipment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ShippingDbContext).Assembly);
}
