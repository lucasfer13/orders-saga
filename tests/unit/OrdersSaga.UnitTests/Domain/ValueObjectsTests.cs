using Orders.Domain;
using Shouldly;

namespace OrdersSaga.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Negative_amount_throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Money(-1m));
    }

    [Fact]
    public void Adding_same_currency_sums_amounts()
    {
        var result = new Money(10m) + new Money(5m);

        result.Amount.ShouldBe(15m);
    }

    [Fact]
    public void Adding_different_currencies_throws()
    {
        Should.Throw<InvalidOperationException>(() => new Money(10m, "EUR") + new Money(5m, "USD"));
    }

    [Fact]
    public void Multiplying_by_quantity_scales_amount()
    {
        var result = new Money(10m) * 3;

        result.Amount.ShouldBe(30m);
    }
}

public class StronglyTypedIdTests
{
    [Fact]
    public void Same_value_order_ids_are_equal()
    {
        var guid = Guid.NewGuid();

        new OrderId(guid).ShouldBe(new OrderId(guid));
    }

    [Fact]
    public void New_order_ids_are_unique()
    {
        OrderId.New().ShouldNotBe(OrderId.New());
    }
}
