using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrdersSaga.IntegrationTests.Infrastructure;
using Payments.Api.Features.ChargePayment;
using Payments.Api.Features.RefundPayment;
using Payments.Api.Persistence;
using Shouldly;

namespace OrdersSaga.IntegrationTests.Persistence;

[Collection(SharedDatabase.Name)]
public class PaymentsPersistenceTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Charging_an_order_survives_the_scope_it_was_created_in()
    {
        var orderId = Guid.NewGuid();

        var result = await Handle<ChargePaymentHandler, ChargePaymentResult>(
            handler => handler.HandleAsync(new ChargePaymentCommand(orderId, 50m, "EUR"), TestContext.Current.CancellationToken));

        result.Succeeded.ShouldBeTrue();

        var stored = await Read(orderId);
        stored.ShouldNotBeNull();
        stored.Id.ShouldBe(result.ChargeId!.Value);
        stored.Amount.ShouldBe(50m);
        stored.Currency.ShouldBe("EUR");
        stored.Refunded.ShouldBeFalse();
    }

    [Fact]
    public async Task Refunding_a_charge_is_recorded()
    {
        var orderId = Guid.NewGuid();

        await Handle<ChargePaymentHandler, ChargePaymentResult>(
            handler => handler.HandleAsync(new ChargePaymentCommand(orderId, 50m, "EUR"), TestContext.Current.CancellationToken));

        var refund = await Handle<RefundPaymentHandler, RefundPaymentResult>(
            handler => handler.HandleAsync(new RefundPaymentCommand(orderId), TestContext.Current.CancellationToken));

        refund.Refunded.ShouldBeTrue();
        (await Read(orderId))!.Refunded.ShouldBeTrue();
    }

    [Fact]
    public async Task Refunding_twice_reports_nothing_to_refund()
    {
        var orderId = Guid.NewGuid();

        await Handle<ChargePaymentHandler, ChargePaymentResult>(
            handler => handler.HandleAsync(new ChargePaymentCommand(orderId, 50m, "EUR"), TestContext.Current.CancellationToken));
        await Handle<RefundPaymentHandler, RefundPaymentResult>(
            handler => handler.HandleAsync(new RefundPaymentCommand(orderId), TestContext.Current.CancellationToken));

        var second = await Handle<RefundPaymentHandler, RefundPaymentResult>(
            handler => handler.HandleAsync(new RefundPaymentCommand(orderId), TestContext.Current.CancellationToken));

        second.Refunded.ShouldBeFalse();
    }

    [Fact]
    public async Task Refunding_an_order_without_a_charge_reports_nothing_to_refund()
    {
        var result = await Handle<RefundPaymentHandler, RefundPaymentResult>(
            handler => handler.HandleAsync(new RefundPaymentCommand(Guid.NewGuid()), TestContext.Current.CancellationToken));

        result.Refunded.ShouldBeFalse();
    }

    /// <summary>The decline is checked before anything is written, so a declined charge leaves no row behind.</summary>
    [Fact]
    public async Task A_charge_above_the_threshold_is_declined_and_leaves_no_row()
    {
        var orderId = Guid.NewGuid();
        var amount = DatabaseFixture.PaymentDeclineThreshold + 1m;

        var result = await Handle<ChargePaymentHandler, ChargePaymentResult>(
            handler => handler.HandleAsync(new ChargePaymentCommand(orderId, amount, "EUR"), TestContext.Current.CancellationToken));

        result.Succeeded.ShouldBeFalse();
        result.DeclineReason.ShouldNotBeNullOrWhiteSpace();
        (await Read(orderId)).ShouldBeNull();
    }

    private async Task<TResult> Handle<THandler, TResult>(Func<THandler, Task<TResult>> act)
        where THandler : notnull
    {
        await using var scope = fixture.Payments.Services.CreateAsyncScope();
        return await act(scope.ServiceProvider.GetRequiredService<THandler>());
    }

    private async Task<Charge?> Read(Guid orderId)
    {
        await using var scope = fixture.Payments.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();

        return await context.Charges
            .AsNoTracking()
            .SingleOrDefaultAsync(charge => charge.OrderId == orderId, TestContext.Current.CancellationToken);
    }
}
