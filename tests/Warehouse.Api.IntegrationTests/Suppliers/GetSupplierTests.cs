using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Application.Suppliers;

namespace Warehouse.Api.IntegrationTests.Suppliers;

public class GetSupplierTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GetSupplierTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GET_SupplierById_ExistingId_ReturnsSupplier_200()
    {
        var response = await _client.GetAsync($"/api/suppliers/{_factory.SeededSupplierOneId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var supplier = await response.Content.ReadFromJsonAsync<SupplierViewModel>();
        supplier.Should().NotBeNull();
        supplier!.Id.Should().Be(_factory.SeededSupplierOneId);
        supplier.Name.Should().Be("Acme Corp");
        supplier.Country.Should().Be("USA");
    }

    [Fact]
    public async Task GET_SupplierById_MadeUpId_Returns404()
    {
        var response = await _client.GetAsync($"/api/suppliers/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
