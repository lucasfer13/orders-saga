using Microsoft.EntityFrameworkCore;
using Payments.Api;
using Payments.Api.Features.ChargePayment;
using Payments.Api.Features.GetPayments;
using Payments.Api.Features.RefundPayment;
using Payments.Api.HealthChecks;
using Payments.Api.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.");

// Scoped, not pooled: pooling is an optimisation, and optimisations here are
// justified with numbers. No retry-on-failure yet either — an execution strategy
// forces every user transaction to be wrapped, and transactions arrive later.
builder.Services.AddDbContext<PaymentsDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<MigrationState>();
builder.Services.AddHostedService<DatabaseMigrator>();

builder.Services.AddOpenApi();
builder.Services.AddServiceHealthChecks();
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection(PaymentsOptions.SectionName));
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
