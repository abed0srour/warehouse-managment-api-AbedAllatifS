using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Products;

public class CreateProductTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CreateProductTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task POST_Products_ValidProduct_Returns201()
    {
        var request = new CreateProductRequest
        {
            Name = "Mechanical Keyboard",
            SKU = "SKU-100",
            Description = "A mechanical keyboard",
            Price = 79.99m,
            QuantityInStock = 15,
            SupplierName = "Acme Corp"
        };

        var response = await _client.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("sku").GetString().Should().Be("SKU-100");
        document.RootElement.GetProperty("quantityInStock").GetInt32().Should().Be(15);
    }

    [Fact]
    public async Task POST_Products_DuplicateSku_Returns409()
    {
        var request = new CreateProductRequest
        {
            Name = "Another Wireless Mouse",
            SKU = CustomWebApplicationFactory.SeededProductOneSku,
            Description = "Duplicate SKU attempt",
            Price = 25.00m,
            QuantityInStock = 5
        };

        var response = await _client.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
