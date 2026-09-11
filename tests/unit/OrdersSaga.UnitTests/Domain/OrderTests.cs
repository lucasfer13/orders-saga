using Orders.Domain;
using Orders.Domain.Events;
using Orders.Domain.Exceptions;
using Shouldly;

namespace OrdersSaga.UnitTests.Domain;

public class OrderTests
{
    private static OrderLine Line(int quantity = 2, decimal unitPrice = 10m) =>
        new(ProductId.New(), quantity, new Money(unitPrice));

    [Fact]
    public void Place_creates_order_in_pending_state_with_correct_total()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line(2, 10m), Line(1, 5m)]);

        order.Status.ShouldBe(OrderStatus.Pending);
        order.Total.Amount.ShouldBe(25m);
        order.DomainEvents.ShouldHaveSingleItem();
        order.DomainEvents.Single().ShouldBeOfType<OrderPlaced>();
    }

    [Fact]
    public void Place_without_lines_throws()
    {
        Should.Throw<InvalidOrderException>(() => Order.Place(OrderId.New(), CustomerId.New(), []));
    }

    [Fact]
    public void MarkStockReserved_from_pending_transitions_and_raises_event()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);
        order.ClearDomainEvents();

        order.MarkStockReserved();

        order.Status.ShouldBe(OrderStatus.StockReserved);
        order.DomainEvents.Single().ShouldBeOfType<OrderStockReserved>();
    }

    [Fact]
    public void MarkStockReserved_when_not_pending_throws()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);
        order.MarkStockReserved();

        Should.Throw<InvalidOrderStateTransitionException>(() => order.MarkStockReserved());
    }

    [Fact]
    public void MarkPaymentCharged_before_stock_reserved_throws()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);

        Should.Throw<InvalidOrderStateTransitionException>(() => order.MarkPaymentCharged());
    }

    [Fact]
    public void Full_happy_path_reaches_shipped()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);

        order.MarkStockReserved();
        order.MarkPaymentCharged();
        order.MarkShipped();

        order.Status.ShouldBe(OrderStatus.Shipped);
    }

    [Fact]
    public void MarkShipped_before_payment_charged_throws()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);
        order.MarkStockReserved();

        Should.Throw<InvalidOrderStateTransitionException>(() => order.MarkShipped());
    }

    [Fact]
    public void BeginCompensation_after_stock_reserved_transitions_to_compensating()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);
        order.MarkStockReserved();

        order.BeginCompensation("pago rechazado");

        order.Status.ShouldBe(OrderStatus.Compensating);
    }

    [Fact]
    public void BeginCompensation_after_payment_charged_transitions_to_compensating()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);
        order.MarkStockReserved();
        order.MarkPaymentCharged();

        order.BeginCompensation("fallo en expedición");

        order.Status.ShouldBe(OrderStatus.Compensating);
    }

    [Fact]
    public void BeginCompensation_from_pending_throws()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);

        Should.Throw<InvalidOrderStateTransitionException>(() => order.BeginCompensation("motivo"));
    }

    [Fact]
    public void BeginCompensation_from_shipped_throws()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);
        order.MarkStockReserved();
        order.MarkPaymentCharged();
        order.MarkShipped();

        Should.Throw<InvalidOrderStateTransitionException>(() => order.BeginCompensation("motivo"));
    }

    [Fact]
    public void Cancel_from_compensating_transitions_and_raises_event()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);
        order.MarkStockReserved();
        order.BeginCompensation("stock insuficiente en otro paso");
        order.ClearDomainEvents();

        order.Cancel("compensación completada");

        order.Status.ShouldBe(OrderStatus.Cancelled);
        order.DomainEvents.Single().ShouldBeOfType<OrderCancelled>();
    }

    [Fact]
    public void Cancel_without_compensating_first_throws()
    {
        var order = Order.Place(OrderId.New(), CustomerId.New(), [Line()]);

        Should.Throw<InvalidOrderStateTransitionException>(() => order.Cancel("motivo"));
    }
}
