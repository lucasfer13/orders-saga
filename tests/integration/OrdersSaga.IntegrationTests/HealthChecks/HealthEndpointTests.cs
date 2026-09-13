using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace OrdersSaga.IntegrationTests.HealthChecks;

/// <summary>
/// Every service exposes the same three probes, so the assertions live here once
/// and each service supplies its own entry point.
/// </summary>
public abstract class HealthEndpointTests<TEntryPoint>(WebApplicationFactory<TEntryPoint> factory)
    : IClassFixture<WebApplicationFactory<TEntryPoint>>
    where TEntryPoint : class
{
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health/startup")]
    public async Task Reports_healthy(string path)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");

        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("status").GetString().ShouldBe("Healthy");
    }

    [Fact]
    public async Task Live_does_not_probe_dependencies()
    {
        using var client = factory.CreateClient();

        var report = await client.GetFromJsonAsync<JsonElement>("/health/live");

        // The rule that matters: a liveness probe that fails when the database is
        // down makes the orchestrator restart a healthy process in a loop.
        var names = report.GetProperty("checks").EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .ToArray();

        names.ShouldBe(["self"]);
    }
}

public class OrdersHealthEndpointTests(WebApplicationFactory<Orders.Api.ApiMarker> factory)
    : HealthEndpointTests<Orders.Api.ApiMarker>(factory);

public class InventoryHealthEndpointTests(WebApplicationFactory<Inventory.Api.ApiMarker> factory)
    : HealthEndpointTests<Inventory.Api.ApiMarker>(factory);

public class PaymentsHealthEndpointTests(WebApplicationFactory<Payments.Api.ApiMarker> factory)
    : HealthEndpointTests<Payments.Api.ApiMarker>(factory);

public class ShippingHealthEndpointTests(WebApplicationFactory<Shipping.Api.ApiMarker> factory)
    : HealthEndpointTests<Shipping.Api.ApiMarker>(factory);
