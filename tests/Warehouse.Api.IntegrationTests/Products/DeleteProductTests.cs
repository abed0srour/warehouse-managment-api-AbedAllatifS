using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Application.Products;

namespace Warehouse.Api.IntegrationTests.Products;

public class DeleteProductTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeleteProductTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DELETE_Product_ArchivesInsteadOfHardDelete_204()
    {
        var response = await _client.DeleteAsync($"/api/products/{_factory.SeededProductOneId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GET_ProductById_AfterDelete_StillReturnsArchivedProduct_200()
    {
        var deleteResponse = await _client.DeleteAsync($"/api/products/{_factory.SeededProductTwoId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/products/{_factory.SeededProductTwoId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await getResponse.Content.ReadFromJsonAsync<ProductViewModel>();
        product.Should().NotBeNull();
        product!.Id.Should().Be(_factory.SeededProductTwoId);
        product.IsArchived.Should().BeTrue();
    }
}
