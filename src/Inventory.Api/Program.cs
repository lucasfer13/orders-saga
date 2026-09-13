using Inventory.Api;
using Inventory.Api.Features.GetStock;
using Inventory.Api.Features.ReleaseStock;
using Inventory.Api.Features.ReserveStock;
using Inventory.Api.HealthChecks;
using Inventory.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.");

// Scoped, not pooled: pooling is an optimisation, and optimisations here are
// justified with numbers. No retry-on-failure yet either — an execution strategy
// forces every user transaction to be wrapped, and the reservation opens one.
builder.Services.AddDbContext<InventoryDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<MigrationState>();
builder.Services.AddScoped<StockSeeder>();
builder.Services.AddHostedService<DatabaseMigrator>();

builder.Services.AddOpenApi();
builder.Services.AddServiceHealthChecks();
builder.Services.Configure<InventoryOptions>(builder.Configuration.GetSection(InventoryOptions.SectionName));
builder.Services.AddScoped<ReserveStockHandler>();
builder.Services.AddScoped<ReleaseStockHandler>();

var app = builder.Build();

// Docs are development-only; docker compose will set the environment so the demo keeps them.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapHealthCheckEndpoints();
app.MapGetStock();

app.Run();
