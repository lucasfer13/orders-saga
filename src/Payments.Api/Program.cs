using Payments.Api;
using Payments.Api.Features.ChargePayment;
using Payments.Api.Features.GetPayments;
using Payments.Api.Features.RefundPayment;
using Payments.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection(PaymentsOptions.SectionName));
builder.Services.AddSingleton<PaymentStore>();
builder.Services.AddScoped<ChargePaymentHandler>();
builder.Services.AddScoped<RefundPaymentHandler>();

var app = builder.Build();

app.MapGetPayments();

app.Run();
