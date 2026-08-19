using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Warehouse.Application.Products;
using Warehouse.Application.Suppliers;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests;

public class FullBusinessFlowTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly HttpClient _client;
    private readonly List<string> _writtenFiles = new();

    public FullBusinessFlowTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullProductLifecycle_CreateAssignUploadUpdateArchive_Succeeds()
    {
        var createSupplierResponse = await _client.PostAsJsonAsync("/api/suppliers", new CreateSupplierRequest
        {
            Name = "E2E Supplier",
            Country = "USA",
            ContactEmail = "e2e-supplier@example.com",
            PhoneNumber = "+15550001111"
        });
        createSupplierResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var supplier = await createSupplierResponse.Content.ReadFromJsonAsync<SupplierViewModel>();
        supplier.Should().NotBeNull();

        var createProductResponse = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "E2E Product",
            SKU = "SKU-E2E-001",
            Description = "A product for the end-to-end flow",
            Price = 10.00m,
            QuantityInStock = 25
        });
        createProductResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        using var createdProductDocument = JsonDocument.Parse(await createProductResponse.Content.ReadAsStringAsync());
        var productId = createdProductDocument.RootElement.GetProperty("id").GetGuid();

        var assignSupplierResponse = await _client.PostAsync(
            $"/api/products/{productId}/assign-supplier/{supplier!.Id}",
            null);
        assignSupplierResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var imagePath = Path.Combine("wwwroot", "uploads", $"{productId}.jpg");
        _writtenFiles.Add(imagePath);
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "photo.jpg");
        var uploadImageResponse = await _client.PostAsync($"/api/products/{productId}/image", form);
        uploadImageResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updateQuantityResponse = await _client.PostAsJsonAsync(
            $"/api/products/{productId}/quantity",
            new UpdateProductQuantityRequest { QuantityInStock = 60 });
        updateQuantityResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var updatePriceResponse = await _client.PostAsJsonAsync(
            $"/api/products/{productId}/price",
            new UpdateProductPriceRequest { Price = 34.99m });
        updatePriceResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var archiveResponse = await _client.DeleteAsync($"/api/products/{productId}");
        archiveResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var finalGetResponse = await _client.GetAsync($"/api/products/{productId}");
        finalGetResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var finalProduct = await finalGetResponse.Content.ReadFromJsonAsync<ProductViewModel>();

        finalProduct.Should().NotBeNull();
        finalProduct!.IsArchived.Should().BeTrue();
        finalProduct.QuantityInStock.Should().Be(60);
        finalProduct.Price.Should().Be(34.99m);
        finalProduct.SupplierName.Should().Be("E2E Supplier");
    }

    public void Dispose()
    {
        foreach (var path in _writtenFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
