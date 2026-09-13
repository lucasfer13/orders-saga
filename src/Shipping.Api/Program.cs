using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Shipping.Api.Features.CancelShipment;
using Shipping.Api.Features.GetShipments;
using Shipping.Api.Features.ShipOrder;
using Shipping.Api.HealthChecks;
using Shipping.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.");

// Scoped, not pooled: pooling is an optimisation, and optimisations here are
// justified with numbers. No retry-on-failure yet either — an execution strategy
// forces every user transaction to be wrapped, and transactions arrive later.
builder.Services.AddDbContext<ShippingDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<MigrationState>();
builder.Services.AddHostedService<DatabaseMigrator>();

builder.Services.AddOpenApi();
builder.Services.AddServiceHealthChecks();
builder.Services.AddScoped<ShipOrderHandler>();
builder.Services.AddScoped<CancelShipmentHandler>();

var app = builder.Build();

// Docs are development-only; docker compose will set the environment so the demo keeps them.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthCheckEndpoints();
app.MapGetShipments();

app.Run();
