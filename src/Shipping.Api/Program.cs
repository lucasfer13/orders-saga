using Scalar.AspNetCore;
using Shipping.Api.Features.CancelShipment;
using Shipping.Api.Features.GetShipments;
using Shipping.Api.Features.ShipOrder;
using Shipping.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddSingleton<ShipmentStore>();
builder.Services.AddScoped<ShipOrderHandler>();
builder.Services.AddScoped<CancelShipmentHandler>();

var app = builder.Build();

// Docs are development-only; docker compose will set the environment so the demo keeps them.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapGetShipments();

app.Run();
