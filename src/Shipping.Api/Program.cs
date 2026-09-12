using Shipping.Api.Features.CancelShipment;
using Shipping.Api.Features.GetShipments;
using Shipping.Api.Features.ShipOrder;
using Shipping.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ShipmentStore>();
builder.Services.AddScoped<ShipOrderHandler>();
builder.Services.AddScoped<CancelShipmentHandler>();

var app = builder.Build();

app.MapGetShipments();

app.Run();
