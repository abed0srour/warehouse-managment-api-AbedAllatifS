using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products;
using Warehouse.Presentation.Contracts;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Products;

/// <summary>
/// POST /api/products/stock-adjustments — relative stock receipts and issues.
/// Asserts the HTTP contract, the DTO validation rules (including the cross-field
/// "reason required on a decrease"), and the persisted level in the database.
/// </summary>
public class AdjustStockEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AdjustStockEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    /// <summary>Creates a product with a known stock level so tests never contend for the seeded rows.</summary>
    private async Task<Guid> CreateProductAsync(int quantity = 20)
    {
        var response = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest
        {
            Name = "Adjustable Product",
            SKU = $"SKU-{Guid.NewGuid():N}".Substring(0, 12),
            Description = "For stock adjustment tests",
            Price = 12.50m,
            QuantityInStock = quantity
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private Task<HttpResponseMessage> AdjustAsync(Guid productId, AdjustmentType type, int quantity, string? reason = null) =>
        _client.PostAsJsonAsync("/api/products/stock-adjustments", new CreateStockAdjustmentRequest
        {
            ProductId = productId,
            Type = type,
            Quantity = quantity,
            Reason = reason
        });

    private async Task<int> PersistedQuantityAsync(Guid productId)
    {
        using var db = _factory.CreateDbContext();
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        return product.QuantityInStock;
    }

    // ---------- happy path ----------

    [Fact]
    public async Task POST_Increase_Returns200()
    {
        var productId = await CreateProductAsync();

        var response = await AdjustAsync(productId, AdjustmentType.Increase, 5);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task POST_Increase_ResponseCarriesTheNewLevel()
    {
        var productId = await CreateProductAsync(quantity: 20);

        var response = await AdjustAsync(productId, AdjustmentType.Increase, 5);

        var product = await response.Content.ReadFromJsonAsync<ProductViewModel>();
        product!.Id.Should().Be(productId);
        product.QuantityInStock.Should().Be(25);
    }

    [Fact]
    public async Task POST_Increase_PersistsTheNewLevel()
    {
        var productId = await CreateProductAsync(quantity: 20);

        await AdjustAsync(productId, AdjustmentType.Increase, 5);

        (await PersistedQuantityAsync(productId)).Should().Be(25);
    }

    [Fact]
    public async Task POST_Decrease_PersistsTheNewLevel()
    {
        var productId = await CreateProductAsync(quantity: 20);

        await AdjustAsync(productId, AdjustmentType.Decrease, 8, "Damaged in transit");

        (await PersistedQuantityAsync(productId)).Should().Be(12);
    }

    [Fact]
    public async Task POST_RepeatedAdjustments_Accumulate()
    {
        // The distinguishing behaviour versus POST /{id}/quantity, which overwrites: two
        // receipts of 5 add 10 rather than one discarding the other.
        var productId = await CreateProductAsync(quantity: 20);

        await AdjustAsync(productId, AdjustmentType.Increase, 5);
        await AdjustAsync(productId, AdjustmentType.Increase, 5);

        (await PersistedQuantityAsync(productId)).Should().Be(30);
    }

    [Fact]
    public async Task POST_DecreaseToExactlyZero_IsAllowed()
    {
        var productId = await CreateProductAsync(quantity: 20);

        var response = await AdjustAsync(productId, AdjustmentType.Decrease, 20, "Cleared out");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PersistedQuantityAsync(productId)).Should().Be(0);
    }

    [Fact]
    public async Task POST_AdjustmentTypeAcceptsTheEnumName()
    {
        // Pins the JsonStringEnumConverter: a raw "Increase" string binds, not just the ordinal.
        var productId = await CreateProductAsync(quantity: 20);

        using var content = new StringContent(
            $$"""{"productId":"{{productId}}","type":"Increase","quantity":3}""",
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await _client.PostAsync("/api/products/stock-adjustments", content);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await PersistedQuantityAsync(productId)).Should().Be(23);
    }

    [Fact]
    public async Task POST_Adjustment_IsVisibleThroughTheGetByIdEndpoint()
    {
        var productId = await CreateProductAsync(quantity: 20);

        await AdjustAsync(productId, AdjustmentType.Increase, 7);

        var response = await _client.GetAsync($"/api/products/{productId}");
        var product = await response.Content.ReadFromJsonAsync<ProductViewModel>();

        // Also proves the handler's cache eviction works: GetProductByIdQuery caches for five
        // minutes, so a stale entry would still report the pre-adjustment level here.
        product!.QuantityInStock.Should().Be(27);
    }

    // ---------- DTO validation ----------

    [Fact]
    public async Task POST_DecreaseWithoutAReason_Returns400()
    {
        var productId = await CreateProductAsync();

        var response = await AdjustAsync(productId, AdjustmentType.Decrease, 5, reason: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_DecreaseWithoutAReason_ExplainsWhichRuleFailed()
    {
        var productId = await CreateProductAsync();

        var response = await AdjustAsync(productId, AdjustmentType.Decrease, 5, reason: "   ");

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("A reason is required when decreasing stock.");
    }

    [Fact]
    public async Task POST_DecreaseWithoutAReason_PersistsNothing()
    {
        var productId = await CreateProductAsync(quantity: 20);

        await AdjustAsync(productId, AdjustmentType.Decrease, 5);

        (await PersistedQuantityAsync(productId)).Should().Be(20);
    }

    [Fact]
    public async Task POST_ZeroQuantity_Returns400()
    {
        var productId = await CreateProductAsync();

        var response = await AdjustAsync(productId, AdjustmentType.Increase, 0);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Quantity must be greater than zero.");
    }

    [Fact]
    public async Task POST_NegativeQuantity_Returns400()
    {
        // The magnitude is always unsigned; direction is carried by Type, so a negative
        // quantity is a malformed request rather than a decrease.
        var productId = await CreateProductAsync();

        var response = await AdjustAsync(productId, AdjustmentType.Increase, -5);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_ReasonOverFiveHundredCharacters_Returns400()
    {
        var productId = await CreateProductAsync();

        var response = await AdjustAsync(productId, AdjustmentType.Decrease, 1, new string('x', 501));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_MalformedJson_Returns400()
    {
        using var content = new StringContent("{ not json at all", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/products/stock-adjustments", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ---------- domain rules ----------

    [Fact]
    public async Task POST_UnknownProduct_Returns404()
    {
        var response = await AdjustAsync(Guid.NewGuid(), AdjustmentType.Increase, 5);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task POST_DecreaseBelowZero_Returns409()
    {
        var productId = await CreateProductAsync(quantity: 20);

        var response = await AdjustAsync(productId, AdjustmentType.Decrease, 21, "Too many");

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_DecreaseBelowZero_LeavesTheLevelUntouched()
    {
        var productId = await CreateProductAsync(quantity: 20);

        await AdjustAsync(productId, AdjustmentType.Decrease, 21, "Too many");

        (await PersistedQuantityAsync(productId)).Should().Be(20);
    }

    [Fact]
    public async Task POST_ArchivedProduct_Returns409()
    {
        var productId = await CreateProductAsync();
        (await _client.DeleteAsync($"/api/products/{productId}")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var response = await AdjustAsync(productId, AdjustmentType.Increase, 5);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ---------- swagger ----------

    [Fact]
    public async Task SwaggerDocument_DescribesTheEndpointAndItsResponseCodes()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // Capital P: the document is generated from the "api/[controller]" template, and the
        // token takes the controller class's casing. Routing itself is case-insensitive, which
        // is why every hand-written call in these tests uses lowercase and still works.
        var operation = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/Products/stock-adjustments")
            .GetProperty("post");

        operation.GetProperty("summary").GetString()
            .Should().Be("Receives stock into, or issues stock out of, a product.");
        operation.GetProperty("responses").TryGetProperty("200", out _).Should().BeTrue();
        operation.GetProperty("responses").TryGetProperty("404", out _).Should().BeTrue();
        operation.GetProperty("responses").TryGetProperty("409", out _).Should().BeTrue();
    }
}
