using Microsoft.EntityFrameworkCore;
using Orders.Api.HealthChecks;
using Orders.Api.Persistence;
using Orders.Domain;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is not configured.");

// Scoped, not pooled: pooling is an optimisation, and optimisations here are
// justified with numbers. No retry-on-failure yet either — an execution strategy
// forces every user transaction to be wrapped, and transactions arrive later.
builder.Services.AddDbContext<OrdersDbContext>(options => options
    .UseNpgsql(connectionString)
    .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton<MigrationState>();
builder.Services.AddHostedService<DatabaseMigrator>();

// The only type of the service that sees OrdersDbContext (ADR-0004).
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

builder.Services.AddServiceHealthChecks();

var app = builder.Build();

app.MapHealthCheckEndpoints();

app.Run();
