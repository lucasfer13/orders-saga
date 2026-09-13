using Orders.Api.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceHealthChecks();

var app = builder.Build();

app.MapHealthCheckEndpoints();

app.Run();
