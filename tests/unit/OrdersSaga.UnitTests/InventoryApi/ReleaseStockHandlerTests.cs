using Inventory.Api.Features.ReleaseStock;
using Inventory.Api.Features.ReserveStock;
using Inventory.Api.Storage;
using Shouldly;

namespace OrdersSaga.UnitTests.InventoryApi;

public class ReleaseStockHandlerTests
{
    private static readonly Guid ProductA = Guid.NewGuid();

    [Fact]
    public void Restores_the_reserved_quantities()
    {
        var orderId = Guid.NewGuid();
        var store = new StockStore();
        store.SetStock(ProductA, 10);
        new ReserveStockHandler(store).Handle(new ReserveStockCommand(orderId, [new ReserveStockLine(ProductA, 4)]));

        var result = new ReleaseStockHandler(store).Handle(new ReleaseStockCommand(orderId));

        result.Released.ShouldBeTrue();
        store.GetAvailable(ProductA).ShouldBe(10);
    }

    [Fact]
    public void Does_nothing_when_the_order_has_no_reservation()
    {
        var store = new StockStore();
        store.SetStock(ProductA, 10);

        var result = new ReleaseStockHandler(store).Handle(new ReleaseStockCommand(Guid.NewGuid()));

        result.Released.ShouldBeFalse();
        store.GetAvailable(ProductA).ShouldBe(10);
    }
}
