using Payments.Api;
using Payments.Api.Features.ChargePayment;
using Payments.Api.Features.GetPayments;
using Payments.Api.Features.RefundPayment;
using Payments.Api.HealthChecks;
using Payments.Api.Storage;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddServiceHealthChecks();
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection(PaymentsOptions.SectionName));
builder.Services.AddSingleton<PaymentStore>();
builder.Services.AddScoped<ChargePaymentHandler>();
builder.Services.AddScoped<RefundPaymentHandler>();

var app = builder.Build();

// Docs are development-only; docker compose will set the environment so the demo keeps them.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthCheckEndpoints();
app.MapGetPayments();

app.Run();
