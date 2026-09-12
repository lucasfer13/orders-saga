using Payments.Api.Features.RefundPayment;
using Payments.Api.Storage;
using Shouldly;

namespace OrdersSaga.UnitTests.PaymentsApi;

public class RefundPaymentHandlerTests
{
    [Fact]
    public void Refunds_an_existing_charge()
    {
        var orderId = Guid.NewGuid();
        var store = new PaymentStore();
        store.Charge(orderId, 50m, "EUR");

        var result = new RefundPaymentHandler(store).Handle(new RefundPaymentCommand(orderId));

        result.Refunded.ShouldBeTrue();
        store.FindCharge(orderId)!.Refunded.ShouldBeTrue();
    }

    [Fact]
    public void Does_nothing_when_the_order_has_no_charge()
    {
        var store = new PaymentStore();

        var result = new RefundPaymentHandler(store).Handle(new RefundPaymentCommand(Guid.NewGuid()));

        result.Refunded.ShouldBeFalse();
    }
}
