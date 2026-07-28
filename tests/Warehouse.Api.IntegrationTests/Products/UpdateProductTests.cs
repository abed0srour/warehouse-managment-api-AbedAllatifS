using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Application.Products;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Products;

public class UpdateProductTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UpdateProductTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task POST_ProductQuantity_ValidQuantity_UpdatesStockAndReflectsOnGet_200()
    {
        var updateResponse = await _client.PostAsJsonAsync(
            $"/api/products/{_factory.SeededProductOneId}/quantity",
            new UpdateProductQuantityRequest { QuantityInStock = 77 });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/products/{_factory.SeededProductOneId}");
        var product = await getResponse.Content.ReadFromJsonAsync<ProductViewModel>();

        product.Should().NotBeNull();
        product!.QuantityInStock.Should().Be(77);
    }

    [Fact]
    public async Task POST_ProductPrice_ValidPrice_UpdatesPriceAndReflectsOnGet_200()
    {
        var updateResponse = await _client.PostAsJsonAsync(
            $"/api/products/{_factory.SeededProductTwoId}/price",
            new UpdateProductPriceRequest { Price = 49.99m });

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/products/{_factory.SeededProductTwoId}");
        var product = await getResponse.Content.ReadFromJsonAsync<ProductViewModel>();

        product.Should().NotBeNull();
        product!.Price.Should().Be(49.99m);
    }
}
