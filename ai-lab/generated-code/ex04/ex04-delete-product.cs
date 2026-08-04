using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products;
using WarehouseManagement.Api.Contracts;
using EfProductimage = Warehouse.Infrastructure.Data.EfModels.Productimage;

namespace Warehouse.Api.IntegrationTests.Products;

public class DeleteProductEndpointTests : IClassFixture<CustomWebApplicationFactory>, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly List<string> _writtenFiles = new();

    public DeleteProductEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task<Guid> CreateProductAsync(string? supplierName = null)
    {
        var request = new CreateProductRequest
        {
            Name = "Disposable Product",
            SKU = $"SKU-{Guid.NewGuid():N}".Substring(0, 12),
            Description = "Created for delete tests",
            Price = 42.00m,
            QuantityInStock = 7,
            SupplierName = supplierName ?? string.Empty
        };

        var response = await _client.PostAsJsonAsync("/api/products", request);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private async Task<string> UploadImageAsync(Guid productId)
    {
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(fileContent, "file", "photo.jpg");

        using (content)
        {
            var response = await _client.PostAsync($"/api/products/{productId}/image", content);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var path = Path.Combine("wwwroot", "uploads", $"{productId}.jpg");
        _writtenFiles.Add(path);
        return path;
    }


    [Fact]
    public async Task DELETE_ExistingProduct_Returns204()
    {
        var id = await CreateProductAsync();

        var response = await _client.DeleteAsync($"/api/products/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DELETE_ExistingProduct_ReturnsEmptyBody()
    {
        var id = await CreateProductAsync();

        var response = await _client.DeleteAsync($"/api/products/{id}");

        (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    [Fact]
    public async Task DELETE_ExistingProduct_EchoesCorrelationIdHeader()
    {
        var id = await CreateProductAsync();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/products/{id}");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be(correlationId);
    }


    [Fact]
    public async Task DELETE_ExistingProduct_DoesNotRemoveTheRow()
    {
        // The row survives. "DELETE" here means archive.
        var id = await CreateProductAsync();

        await _client.DeleteAsync($"/api/products/{id}");

        using var db = _factory.CreateDbContext();
        (await db.Products.AsNoTracking().AnyAsync(p => p.Id == id)).Should().BeTrue();
    }

    [Fact]
    public async Task DELETE_ExistingProduct_SetsIsArchivedInDatabase()
    {
        var id = await CreateProductAsync();

        await _client.DeleteAsync($"/api/products/{id}");

        using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking().SingleAsync(p => p.Id == id);
        persisted.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task DELETE_ExistingProduct_StampsLastUpdatedAt()
    {
        var id = await CreateProductAsync();

        using (var before = _factory.CreateDbContext())
        {
            (await before.Products.AsNoTracking().SingleAsync(p => p.Id == id))
                .LastUpdatedAt.Should().BeNull();
        }

        await _client.DeleteAsync($"/api/products/{id}");

        using var after = _factory.CreateDbContext();
        var persisted = await after.Products.AsNoTracking().SingleAsync(p => p.Id == id);
        persisted.LastUpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DELETE_ExistingProduct_PreservesEveryOtherField()
    {
        var id = await CreateProductAsync();

        await _client.DeleteAsync($"/api/products/{id}");

        using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking().SingleAsync(p => p.Id == id);
        persisted.Name.Should().Be("Disposable Product");
        persisted.Price.Should().Be(42.00m);
        persisted.QuantityInStock.Should().Be(7);
        persisted.Description.Should().Be("Created for delete tests");
    }

    [Fact]
    public async Task DELETE_ExistingProduct_LeavesTotalRowCountUnchanged()
    {
        var id = await CreateProductAsync();

        int before;
        using (var db = _factory.CreateDbContext())
        {
            before = await db.Products.AsNoTracking().CountAsync();
        }

        await _client.DeleteAsync($"/api/products/{id}");

        using var after = _factory.CreateDbContext();
        (await after.Products.AsNoTracking().CountAsync()).Should().Be(before);
    }


    [Fact]
    public async Task GET_ById_AfterDelete_StillReturns200WithArchivedFlag()
    {
        var id = await CreateProductAsync();
        await _client.DeleteAsync($"/api/products/{id}");

        var response = await _client.GetAsync($"/api/products/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await response.Content.ReadFromJsonAsync<ProductViewModel>();
        product!.Id.Should().Be(id);
        product.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task GET_OnlyAvailable_AfterDelete_ExcludesTheProduct()
    {
        var id = await CreateProductAsync();
        await _client.DeleteAsync($"/api/products/{id}");

        var response = await _client.GetAsync("/api/products?onlyAvailable=true");

        var products = await response.Content.ReadFromJsonAsync<List<ProductViewModel>>();
        products.Should().NotContain(p => p.Id == id);
    }

    [Fact]
    public async Task GET_All_AfterDelete_StillIncludesTheProduct()
    {
        var id = await CreateProductAsync();
        await _client.DeleteAsync($"/api/products/{id}");

        var response = await _client.GetAsync("/api/products");

        var products = await response.Content.ReadFromJsonAsync<List<ProductViewModel>>();
        products.Should().Contain(p => p.Id == id);
    }


    [Fact]
    public async Task DELETE_Product_LeavesUploadedImageFileOnDisk()
    {

        var id = await CreateProductAsync();
        var imagePath = await UploadImageAsync(id);
        File.Exists(imagePath).Should().BeTrue();

        await _client.DeleteAsync($"/api/products/{id}");

        File.Exists(imagePath).Should().BeTrue();
    }

    [Fact]
    public async Task DELETE_Product_LeavesProductImageRowsIntact()
    {
        var id = await CreateProductAsync();

        using (var seed = _factory.CreateDbContext())
        {
            seed.Productimages.Add(new EfProductimage
            {
                Id = Guid.NewGuid(),
                ProductId = id,
                FileName = $"{id}.jpg",
                FilePath = $"/uploads/{id}.jpg"
            });
            await seed.SaveChangesAsync();
        }

        await _client.DeleteAsync($"/api/products/{id}");

        using var db = _factory.CreateDbContext();
        (await db.Productimages.AsNoTracking().CountAsync(i => i.ProductId == id)).Should().Be(1);
    }

    [Fact]
    public async Task DELETE_Product_DoesNotTouchItsSupplier()
    {
        var id = await CreateProductAsync(supplierName: "Acme Corp");

        await _client.DeleteAsync($"/api/products/{id}");

        using var db = _factory.CreateDbContext();
        var supplier = await db.Suppliers.AsNoTracking()
            .SingleAsync(s => s.SupplierId == _factory.SeededSupplierOneId);
        supplier.IsActive.Should().BeTrue();
        supplier.Name.Should().Be("Acme Corp");
    }


    [Fact]
    public async Task DELETE_UnknownProduct_Returns404()
    {
        var response = await _client.DeleteAsync($"/api/products/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_UnknownProduct_ReturnsMessageBody()
    {
        var unknownId = Guid.NewGuid();

        var response = await _client.DeleteAsync($"/api/products/{unknownId}");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("message").GetString().Should().Contain(unknownId.ToString());
    }

    [Fact]
    public async Task DELETE_MalformedId_Returns404FromRouteConstraint()
    {
        var response = await _client.DeleteAsync("/api/products/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DELETE_AlreadyArchivedProduct_Returns204Again()
    {

        var id = await CreateProductAsync();
        (await _client.DeleteAsync($"/api/products/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await _client.DeleteAsync($"/api/products/{id}");

        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var db = _factory.CreateDbContext();
        (await db.Products.AsNoTracking().SingleAsync(p => p.Id == id)).IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task DELETE_Product_ThenUploadImage_StillSucceeds()
{
        var id = await CreateProductAsync();
        await _client.DeleteAsync($"/api/products/{id}");

        var imagePath = await UploadImageAsync(id);

        File.Exists(imagePath).Should().BeTrue();
    }

    public void Dispose()
    {
        foreach (var path in _writtenFiles.Where(File.Exists))
        {
            File.Delete(path);
        }
    }
}
