using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Application.Suppliers;

namespace Warehouse.Api.IntegrationTests.Suppliers;

public class DeactivateSupplierTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DeactivateSupplierTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task DELETE_Supplier_DeactivatesInsteadOfHardDelete_204()
    {
        var deleteResponse = await _client.DeleteAsync($"/api/suppliers/{_factory.SeededSupplierOneId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await _client.GetAsync($"/api/suppliers/{_factory.SeededSupplierOneId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var supplier = await getResponse.Content.ReadFromJsonAsync<SupplierViewModel>();
        supplier.Should().NotBeNull();
        supplier!.Id.Should().Be(_factory.SeededSupplierOneId);
        supplier.IsActive.Should().BeFalse();
    }
}
