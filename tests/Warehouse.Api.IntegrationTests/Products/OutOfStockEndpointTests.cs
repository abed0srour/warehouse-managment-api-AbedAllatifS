using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Warehouse.Application.Products.Queries;
using Warehouse.Presentation.Contracts;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Products;

/// <summary>
/// GET /api/products/out-of-stock — the reorder list.
/// Each test drives a product to zero through the API rather than seeding the table, so the
/// endpoint is exercised against state the application itself produced.
/// </summary>
public class OutOfStockEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public OutOfStockEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private static string NewSku() => $"SKU-{Guid.NewGuid():N}".Substring(0, 12);

    private async Task<(Guid Id, string Sku)> CreateProductAsync(string name, int quantity)
    {
        var sku = NewSku();
        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = name,
            SKU = sku,
            Description = "For out-of-stock tests",
            Price = 12.50m,
            QuantityInStock = quantity
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (document.RootElement.GetProperty("id").GetGuid(), sku);
    }

    private async Task<List<OutOfStockProductDto>> GetOutOfStockAsync()
    {
        var response = await _client.GetAsync("/api/products/out-of-stock");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<List<OutOfStockProductDto>>())!;
    }

    // ---------- contract ----------

    [Fact]
    public async Task GET_Returns200()
    {
        var response = await _client.GetAsync("/api/products/out-of-stock");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_ReturnsJsonArray()
    {
        var response = await _client.GetAsync("/api/products/out-of-stock");

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GET_RouteIsNotSwallowedByTheGetByIdRoute()
    {
        // "out-of-stock" is not a Guid, so the {id:guid} constraint must not claim it.
        var response = await _client.GetAsync("/api/products/out-of-stock");

        response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
    }

    // ---------- behaviour ----------

    [Fact]
    public async Task GET_IncludesAProductCreatedAtZeroStock()
    {
        var (id, sku) = await CreateProductAsync("Zero Stock Widget", quantity: 0);

        var products = await GetOutOfStockAsync();

        products.Should().Contain(p => p.Id == id).Which.Sku.Should().Be(sku);
    }

    [Fact]
    public async Task GET_ExcludesAProductWithStockOnHand()
    {
        var (id, _) = await CreateProductAsync("Well Stocked Widget", quantity: 25);

        var products = await GetOutOfStockAsync();

        products.Should().NotContain(p => p.Id == id);
    }

    [Fact]
    public async Task GET_IncludesAProductDrivenToZeroByAStockAdjustment()
    {
        var (id, _) = await CreateProductAsync("Drained Widget", quantity: 6);

        var adjustment = await _client.PostAsJsonAsync("/api/products/stock-adjustments", new CreateStockAdjustmentRequest
        {
            ProductId = id,
            Type = AdjustmentType.Decrease,
            Quantity = 6,
            Reason = "Shipped the last of them"
        });
        adjustment.StatusCode.Should().Be(HttpStatusCode.OK);

        var products = await GetOutOfStockAsync();

        products.Should().Contain(p => p.Id == id);
    }

    [Fact]
    public async Task GET_DropsAProductOnceItIsRestocked()
    {
        var (id, _) = await CreateProductAsync("Restocked Widget", quantity: 0);
        (await GetOutOfStockAsync()).Should().Contain(p => p.Id == id);

        await _client.PostAsJsonAsync("/api/products/stock-adjustments", new CreateStockAdjustmentRequest
        {
            ProductId = id,
            Type = AdjustmentType.Increase,
            Quantity = 10
        });

        (await GetOutOfStockAsync()).Should().NotContain(p => p.Id == id);
    }

    [Fact]
    public async Task GET_ExcludesArchivedProducts()
    {
        var (id, _) = await CreateProductAsync("Archived Widget", quantity: 0);
        (await GetOutOfStockAsync()).Should().Contain(p => p.Id == id);

        (await _client.DeleteAsync($"/api/products/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await GetOutOfStockAsync()).Should().NotContain(p => p.Id == id);
    }

    [Fact]
    public async Task GET_CarriesTheSupplierNameForReordering()
    {
        var sku = NewSku();
        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Sourced Widget",
            SKU = sku,
            Description = "For out-of-stock tests",
            Price = 12.50m,
            QuantityInStock = 0,
            SupplierName = "Acme Corp"
        });
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetGuid();

        var products = await GetOutOfStockAsync();

        var dto = products.Should().ContainSingle(p => p.Id == id).Subject;
        dto.SupplierName.Should().Be("Acme Corp");
        // GAP pinned elsewhere: create copies SupplierName as free text and never resolves it,
        // so the FK stays null even though "Acme Corp" exists as a supplier row.
        dto.SupplierId.Should().BeNull();
    }

    [Fact]
    public async Task GET_ResultsAreOrderedByName()
    {
        await CreateProductAsync("ZZZ Ordering Probe", quantity: 0);
        await CreateProductAsync("AAA Ordering Probe", quantity: 0);

        var products = await GetOutOfStockAsync();

        // Only the two probes are asserted on, and they sort the same way under every
        // comparer. Asserting the whole list would pin a collation: the InMemory provider
        // orders culture-aware ("Zero" before "ZZZ") where Postgres depends on the database's
        // own collation, so a full-list assertion would be testing the provider, not the query.
        var probes = products.Select(p => p.Name).Where(n => n.EndsWith("Ordering Probe")).ToList();
        probes.Should().ContainInOrder("AAA Ordering Probe", "ZZZ Ordering Probe");
    }

    [Fact]
    public async Task GET_SeededProductsAreNotReported_BecauseTheyHaveStock()
    {
        // The factory seeds two products with 50 and 200 units.
        var products = await GetOutOfStockAsync();

        products.Should().NotContain(p => p.Sku == CustomWebApplicationFactory.SeededProductOneSku);
        products.Should().NotContain(p => p.Sku == CustomWebApplicationFactory.SeededProductTwoSku);
    }

    // ---------- swagger ----------

    [Fact]
    public async Task SwaggerDocument_DescribesTheEndpoint()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/Products/out-of-stock")
            .GetProperty("get");

        operation.GetProperty("summary").GetString()
            .Should().Be("Lists every product that has run down to zero stock.");
    }
}
