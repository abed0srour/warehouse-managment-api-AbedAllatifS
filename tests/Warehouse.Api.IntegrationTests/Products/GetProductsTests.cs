using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Application.Products;

namespace Warehouse.Api.IntegrationTests.Products;

public class GetProductsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GetProductsTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_Products_ReturnsSeededProducts_200()
    {
        var response = await _client.GetAsync("/api/products");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductViewModel>>();
        products.Should().NotBeNull();
        products!.Select(p => p.Sku).Should().Contain(new[] { CustomWebApplicationFactory.SeededProductOneSku, CustomWebApplicationFactory.SeededProductTwoSku });
    }

    [Fact]
    public async Task GET_ProductById_ExistingId_ReturnsProduct_200()
    {
        var response = await _client.GetAsync($"/api/products/{_factory.SeededProductOneId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<ProductViewModel>();
        product.Should().NotBeNull();
        product!.Id.Should().Be(_factory.SeededProductOneId);
        product.Sku.Should().Be(CustomWebApplicationFactory.SeededProductOneSku);
    }

    [Fact]
    public async Task GET_ProductById_MadeUpId_Returns404()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GET_ProductsSearch_ByName_ReturnsMatches_200()
    {
        var response = await _client.GetAsync("/api/products/search?name=Wireless");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<List<ProductViewModel>>();
        products.Should().NotBeNull();
        products!.Should().Contain(p => p.Sku == CustomWebApplicationFactory.SeededProductOneSku);
    }

    [Fact]
    public async Task GET_ProductsSearch_NoFilters_Returns400()
    {
        var response = await _client.GetAsync("/api/products/search");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
