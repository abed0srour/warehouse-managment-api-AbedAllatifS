using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Application.Products;

namespace Warehouse.Api.IntegrationTests.Suppliers;

public class AssignSupplierTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AssignSupplierTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task POST_AssignSupplier_ValidProductAndSupplier_ReflectsOnProductGet_200()
    {
        var assignResponse = await _client.PostAsync(
            $"/api/products/{_factory.SeededProductOneId}/assign-supplier/{_factory.SeededSupplierTwoId}",
            null);

        assignResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var getResponse = await _client.GetAsync($"/api/products/{_factory.SeededProductOneId}");
        var product = await getResponse.Content.ReadFromJsonAsync<ProductViewModel>();

        product.Should().NotBeNull();
        product!.SupplierName.Should().Be("Globex Supplies");
    }
}
