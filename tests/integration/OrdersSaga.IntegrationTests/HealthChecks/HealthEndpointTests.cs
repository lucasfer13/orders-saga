using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using OrdersSaga.IntegrationTests.Infrastructure;
using Shouldly;

namespace OrdersSaga.IntegrationTests.HealthChecks;

/// <summary>
/// Every service exposes the same three probes, so the assertions live here once
/// and each service supplies its own client.
/// </summary>
public abstract class HealthEndpointTests
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health/startup")]
    public async Task Reports_healthy(string path)
    {
        using var client = CreateClient();

        var response = await client.GetAsync(path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var report = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        report.GetProperty("status").GetString().ShouldBe("Healthy");
    }

    [Fact]
    public async Task Live_does_not_probe_dependencies()
    {
        // The rule that matters: a liveness probe that fails when the database is
        // down makes the orchestrator restart a healthy process in a loop.
        (await ChecksIn("/health/live")).ShouldBe(["self"]);
    }

    protected abstract HttpClient CreateClient();

    protected async Task<string?[]> ChecksIn(string path)
    {
        using var client = CreateClient();

        var report = await client.GetFromJsonAsync<JsonElement>(path, cancellationToken: TestContext.Current.CancellationToken);

        return [.. report.GetProperty("checks").EnumerateArray().Select(check => check.GetProperty("name").GetString())];
    }
}

/// <summary>Adds the probes that only mean something once the service owns a database.</summary>
public abstract class PersistedServiceHealthEndpointTests : HealthEndpointTests
{
    [Fact]
    public async Task Ready_probes_the_database_and_the_migrations()
    {
        (await ChecksIn("/health/ready")).ShouldBe(["postgres", "migrations"], ignoreOrder: true);
    }

    [Fact]
    public async Task Startup_probes_the_migrations()
    {
        (await ChecksIn("/health/startup")).ShouldBe(["migrations"]);
    }
}

public class OrdersHealthEndpointTests(WebApplicationFactory<Orders.Api.ApiMarker> factory)
    : HealthEndpointTests, IClassFixture<WebApplicationFactory<Orders.Api.ApiMarker>>
{
    protected override HttpClient CreateClient() => factory.CreateClient();
}

public class InventoryHealthEndpointTests(WebApplicationFactory<Inventory.Api.ApiMarker> factory)
    : HealthEndpointTests, IClassFixture<WebApplicationFactory<Inventory.Api.ApiMarker>>
{
    protected override HttpClient CreateClient() => factory.CreateClient();
}

[Collection(SharedDatabase.Name)]
public class PaymentsHealthEndpointTests(DatabaseFixture fixture) : PersistedServiceHealthEndpointTests
{
    protected override HttpClient CreateClient() => fixture.Payments.CreateClient();
}

[Collection(SharedDatabase.Name)]
public class ShippingHealthEndpointTests(DatabaseFixture fixture) : PersistedServiceHealthEndpointTests
{
    protected override HttpClient CreateClient() => fixture.Shipping.CreateClient();
}
