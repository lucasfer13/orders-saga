using Microsoft.Extensions.Options;
using Payments.Api;
using Payments.Api.Features.ChargePayment;
using Payments.Api.Storage;
using Shouldly;

namespace OrdersSaga.UnitTests.PaymentsApi;

public class ChargePaymentHandlerTests
{
    private static ChargePaymentHandler HandlerWith(PaymentStore store, decimal? declineAbove) =>
        new(store, Options.Create(new PaymentsOptions { DeclineAboveAmount = declineAbove }));

    [Fact]
    public void Charges_when_the_amount_is_within_the_threshold()
    {
        var orderId = Guid.NewGuid();
        var store = new PaymentStore();

        var result = HandlerWith(store, declineAbove: 100m)
            .Handle(new ChargePaymentCommand(orderId, 50m, "EUR"));

        result.Succeeded.ShouldBeTrue();
        store.FindCharge(orderId).ShouldNotBeNull();
    }

    [Fact]
    public void Declines_when_the_amount_is_above_the_threshold()
    {
        var orderId = Guid.NewGuid();
        var store = new PaymentStore();

        var result = HandlerWith(store, declineAbove: 100m)
            .Handle(new ChargePaymentCommand(orderId, 150m, "EUR"));

        result.Succeeded.ShouldBeFalse();
        result.DeclineReason.ShouldNotBeNullOrWhiteSpace();
        store.FindCharge(orderId).ShouldBeNull();
    }

    [Fact]
    public void Charges_when_no_threshold_is_configured()
    {
        var orderId = Guid.NewGuid();
        var store = new PaymentStore();

        var result = HandlerWith(store, declineAbove: null)
            .Handle(new ChargePaymentCommand(orderId, 10_000m, "EUR"));

        result.Succeeded.ShouldBeTrue();
    }
}
