using Inventory.Api.Features.GetStock;
using Inventory.Api.Features.ReleaseStock;
using Inventory.Api.Features.ReserveStock;
using Inventory.Api.Storage;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<StockStore>();
builder.Services.AddScoped<ReserveStockHandler>();
builder.Services.AddScoped<ReleaseStockHandler>();

var app = builder.Build();

app.MapGetStock();

app.Run();
