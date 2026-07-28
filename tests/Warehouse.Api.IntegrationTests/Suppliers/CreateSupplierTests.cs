using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Application.Suppliers;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Suppliers;

public class CreateSupplierTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public CreateSupplierTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task POST_Suppliers_ValidSupplier_Returns201()
    {
        var request = new CreateSupplierRequest
        {
            Name = "Initech Parts",
            Country = "USA",
            ContactEmail = "orders@initech.com",
            PhoneNumber = "+15550123456"
        };

        var response = await _client.PostAsJsonAsync("/api/suppliers", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplier = await response.Content.ReadFromJsonAsync<SupplierViewModel>();
        supplier.Should().NotBeNull();
        supplier!.Name.Should().Be("Initech Parts");
        supplier.IsActive.Should().BeTrue();
    }
}
