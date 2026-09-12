using Inventory.Api.Features.ReserveStock;
using Inventory.Api.Storage;
using Shouldly;

namespace OrdersSaga.UnitTests.InventoryApi;

public class ReserveStockHandlerTests
{
    private static readonly Guid ProductA = Guid.NewGuid();
    private static readonly Guid ProductB = Guid.NewGuid();

    [Fact]
    public void Reserves_when_there_is_enough_stock()
    {
        var store = new StockStore();
        store.SetStock(ProductA, 10);
        var handler = new ReserveStockHandler(store);

        var result = handler.Handle(new ReserveStockCommand(Guid.NewGuid(), [new ReserveStockLine(ProductA, 3)]));

        result.Succeeded.ShouldBeTrue();
        store.GetAvailable(ProductA).ShouldBe(7);
    }

    [Fact]
    public void Fails_when_stock_is_insufficient()
    {
        var store = new StockStore();
        store.SetStock(ProductA, 2);
        var handler = new ReserveStockHandler(store);

        var result = handler.Handle(new ReserveStockCommand(Guid.NewGuid(), [new ReserveStockLine(ProductA, 3)]));

        result.Succeeded.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNullOrWhiteSpace();
        store.GetAvailable(ProductA).ShouldBe(2);
    }

    [Fact]
    public void Fails_when_the_product_is_unknown()
    {
        var handler = new ReserveStockHandler(new StockStore());

        var result = handler.Handle(new ReserveStockCommand(Guid.NewGuid(), [new ReserveStockLine(ProductA, 1)]));

        result.Succeeded.ShouldBeFalse();
    }

    [Fact]
    public void Reserves_nothing_when_one_line_cannot_be_satisfied()
    {
        var store = new StockStore();
        store.SetStock(ProductA, 10);
        store.SetStock(ProductB, 1);
        var handler = new ReserveStockHandler(store);

        var result = handler.Handle(new ReserveStockCommand(
            Guid.NewGuid(),
            [new ReserveStockLine(ProductA, 5), new ReserveStockLine(ProductB, 5)]));

        result.Succeeded.ShouldBeFalse();
        store.GetAvailable(ProductA).ShouldBe(10);
        store.GetAvailable(ProductB).ShouldBe(1);
    }
}
