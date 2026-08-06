using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products;
using WarehouseManagement.Api.Contracts;

namespace Warehouse.Api.IntegrationTests.Products;
public class CreateProductEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CreateProductEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static CreateProductRequest ValidRequest(string sku) => new()
    {
        Name = "Mechanical Keyboard",
        SKU = sku,
        Description = "A tactile mechanical keyboard",
        Price = 79.99m,
        QuantityInStock = 15,
        SupplierName = "Acme Corp"
    };

    private static string NewSku() => $"SKU-{Guid.NewGuid():N}".Substring(0, 12);

    [Fact]
    public async Task POST_ValidProduct_Returns201()
    {
        var response = await _client.PostAsJsonAsync("/api/products", ValidRequest(NewSku()));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task POST_ValidProduct_SetsLocationHeaderToTheNewResource()
    {
        var response = await _client.PostAsJsonAsync("/api/products", ValidRequest(NewSku()));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var id = document.RootElement.GetProperty("id").GetGuid();

        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Be($"http://localhost/api/Products/{id}");
    }

    [Fact]
    public async Task POST_ValidProduct_ReturnsJsonContentType()
    {
        var response = await _client.PostAsJsonAsync("/api/products", ValidRequest(NewSku()));

        response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task POST_ValidProduct_EchoesCorrelationIdHeader()
    {
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/products")
        {
            Content = JsonContent.Create(ValidRequest(NewSku()))
        };
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await _client.SendAsync(request);

        response.Headers.GetValues("X-Correlation-Id").Should().ContainSingle().Which.Should().Be(correlationId);
    }

    [Fact]
    public async Task POST_ValidProduct_GeneratesCorrelationIdWhenCallerOmitsOne()
    {
        var response = await _client.PostAsJsonAsync("/api/products", ValidRequest(NewSku()));

        response.Headers.TryGetValues("X-Correlation-Id", out var values).Should().BeTrue();
        values!.Single().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task POST_ValidProduct_ResponseBodyMatchesWhatWasSent()
    {
        var sku = NewSku();
        var request = ValidRequest(sku);

        var response = await _client.PostAsJsonAsync("/api/products", request);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("name").GetString().Should().Be(request.Name);
        root.GetProperty("sku").GetString().Should().Be(sku);
        root.GetProperty("description").GetString().Should().Be(request.Description);
        root.GetProperty("price").GetDecimal().Should().Be(request.Price);
        root.GetProperty("quantityInStock").GetInt32().Should().Be(request.QuantityInStock);
        root.GetProperty("supplierName").GetString().Should().Be(request.SupplierName);
    }

    [Fact]
    public async Task POST_ValidProduct_ResponseCarriesServerAssignedFields()
    {
        var response = await _client.PostAsJsonAsync("/api/products", ValidRequest(NewSku()));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("id").GetGuid().Should().NotBeEmpty();
        root.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        root.GetProperty("createdAt").GetDateTime().Should().BeAfter(DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task POST_TwoProducts_ReceiveDistinctIds()
    {
        var first = await _client.PostAsJsonAsync("/api/products", ValidRequest(NewSku()));
        var second = await _client.PostAsJsonAsync("/api/products", ValidRequest(NewSku()));

        using var firstDoc = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var secondDoc = JsonDocument.Parse(await second.Content.ReadAsStringAsync());

        firstDoc.RootElement.GetProperty("id").GetGuid()
            .Should().NotBe(secondDoc.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task POST_ValidProduct_PersistsRowToDatabase()
    {
        var sku = NewSku();
        var request = ValidRequest(sku);

        await _client.PostAsJsonAsync("/api/products", request);

        using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Sku == sku);

        persisted.Should().NotBeNull();
        persisted!.Name.Should().Be(request.Name);
        persisted.Description.Should().Be(request.Description);
        persisted.Price.Should().Be(request.Price);
        persisted.QuantityInStock.Should().Be(request.QuantityInStock);
        persisted.SupplierName.Should().Be(request.SupplierName);
        persisted.IsArchived.Should().BeFalse();
    }

    [Fact]
    public async Task POST_ValidProduct_PersistedIdMatchesResponseId()
    {
        var sku = NewSku();

        var response = await _client.PostAsJsonAsync("/api/products", ValidRequest(sku));
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var returnedId = document.RootElement.GetProperty("id").GetGuid();

        using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking().SingleAsync(p => p.Sku == sku);

        persisted.Id.Should().Be(returnedId);
    }

    [Fact]
    public async Task POST_ValidProduct_LocationHeaderResolvesToTheCreatedProduct()
    {
        var sku = NewSku();
        var response = await _client.PostAsJsonAsync("/api/products", ValidRequest(sku));

        var followUp = await _client.GetAsync(response.Headers.Location);

        followUp.StatusCode.Should().Be(HttpStatusCode.OK);
        var product = await followUp.Content.ReadFromJsonAsync<ProductViewModel>();
        product!.Sku.Should().Be(sku);
    }

    [Fact]
    public async Task POST_ProductWithExpiryDate_PersistsUnspecifiedKind()
    {
        var sku = NewSku();
        var request = ValidRequest(sku);
        request.ExpiryDate = new DateTime(2027, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        await _client.PostAsJsonAsync("/api/products", request);

        using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking().SingleAsync(p => p.Sku == sku);

        persisted.ExpiryDate.Should().NotBeNull();
        persisted.ExpiryDate!.Value.Kind.Should().Be(DateTimeKind.Unspecified);
        persisted.ExpiryDate.Value.Date.Should().Be(new DateTime(2027, 6, 15));
    }

    [Fact]
    public async Task POST_ValidProduct_DoesNotLinkSupplierIdEvenWhenSupplierNameMatches()
    {

        var sku = NewSku();
        var request = ValidRequest(sku);
        request.SupplierName = "Acme Corp";

        await _client.PostAsJsonAsync("/api/products", request);

        using var db = _factory.CreateDbContext();
        var persisted = await db.Products.AsNoTracking().SingleAsync(p => p.Sku == sku);

        persisted.SupplierName.Should().Be("Acme Corp");
        persisted.SupplierId.Should().BeNull();
    }

    [Fact]
    public async Task POST_ValidProduct_IsVisibleThroughTheListEndpoint()
    {
        var sku = NewSku();
        await _client.PostAsJsonAsync("/api/products", ValidRequest(sku));

        var listResponse = await _client.GetAsync("/api/products");

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var products = await listResponse.Content.ReadFromJsonAsync<List<ProductViewModel>>();
        products.Should().Contain(p => p.Sku == sku);
    }

    [Fact]
    public async Task POST_DuplicateSku_Returns409()
    {
        var sku = NewSku();
        await _client.PostAsJsonAsync("/api/products", ValidRequest(sku));

        var duplicate = await _client.PostAsJsonAsync("/api/products", ValidRequest(sku));

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task POST_DuplicateSku_DoesNotPersistASecondRow()
    {
        var sku = NewSku();
        await _client.PostAsJsonAsync("/api/products", ValidRequest(sku));

        await _client.PostAsJsonAsync("/api/products", ValidRequest(sku));

        using var db = _factory.CreateDbContext();
        var count = await db.Products.AsNoTracking().CountAsync(p => p.Sku == sku);

        count.Should().Be(1);
    }

    [Fact]
    public async Task POST_ZeroPrice_Returns400AndPersistsNothing()
    {
        var sku = NewSku();
        var request = ValidRequest(sku);
        request.Price = 0m;

        var response = await _client.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var db = _factory.CreateDbContext();
        (await db.Products.AsNoTracking().AnyAsync(p => p.Sku == sku)).Should().BeFalse();
    }

    [Fact]
    public async Task POST_NegativeQuantity_Returns400AndPersistsNothing()
    {
        var sku = NewSku();
        var request = ValidRequest(sku);
        request.QuantityInStock = -1;

        var response = await _client.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var db = _factory.CreateDbContext();
        (await db.Products.AsNoTracking().AnyAsync(p => p.Sku == sku)).Should().BeFalse();
    }

    [Fact]
    public async Task POST_MissingName_Returns400()
    {
        var request = ValidRequest(NewSku());
        request.Name = string.Empty;

        var response = await _client.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_MissingSku_Returns400()
    {
        var request = ValidRequest(NewSku());
        request.SKU = string.Empty;

        var response = await _client.PostAsJsonAsync("/api/products", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task POST_MalformedJson_Returns400()
    {
        using var content = new StringContent("{ not json at all", System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/products", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
