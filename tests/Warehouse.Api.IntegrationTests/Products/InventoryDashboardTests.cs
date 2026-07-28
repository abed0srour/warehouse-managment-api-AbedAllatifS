using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Application.Inventory.Queries;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Products;

public class InventoryDashboardTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InventoryDashboardTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_InventoryDashboard_SeededProductsAboveThreshold_ReturnsTotalsWithNoLowStock_200()
    {
        var response = await _client.GetAsync("/api/inventory/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<InventoryDashboardDto>();
        dashboard.Should().NotBeNull();
        dashboard!.TotalProducts.Should().Be(2);
        dashboard.TotalSuppliers.Should().Be(2);
        dashboard.LowStockProducts.Should().BeEmpty();
    }

    [Fact]
    public async Task GET_InventoryDashboard_AfterQuantityDropsBelowThreshold_IncludesProductInLowStock_200()
    {
        var updateResponse = await _client.PostAsJsonAsync(
            $"/api/products/{_factory.SeededProductOneId}/quantity",
            new UpdateProductQuantityRequest { QuantityInStock = 3 });
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await _client.GetAsync("/api/inventory/dashboard");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var dashboard = await response.Content.ReadFromJsonAsync<InventoryDashboardDto>();
        dashboard.Should().NotBeNull();
        dashboard!.LowStockProducts.Should().Contain(p => p.Id == _factory.SeededProductOneId && p.QuantityInStock == 3);
    }
}
