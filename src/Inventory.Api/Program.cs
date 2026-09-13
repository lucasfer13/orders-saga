using Inventory.Api.Features.GetStock;
using Inventory.Api.Features.ReleaseStock;
using Inventory.Api.Features.ReserveStock;
using Inventory.Api.HealthChecks;
using Inventory.Api.Storage;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddServiceHealthChecks();
builder.Services.AddSingleton<StockStore>();
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
