using System.Text;
using System.Text.Json;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Warehouse.Application.Products;
using Warehouse.Application.Products.Queries;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Products;

public class GetAllProductsQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly GetAllProductsQueryHandler _handler;

    public GetAllProductsQueryHandlerTests()
    {
        // Default to a cache MISS. Moq's default for byte[] is an empty array rather than
        // null, and GetStringAsync only null-checks -- so without this the handler would
        // receive "" and fail to deserialize it. See Handle_ZeroLengthCacheEntry_Throws.
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mapper.Setup(m => m.Map<IEnumerable<ProductViewModel>>(It.IsAny<object>()))
            .Returns((object source) => ((IEnumerable<Product>)source).Select(ToViewModel).ToList());

        _handler = new GetAllProductsQueryHandler(_productRepository.Object, _mapper.Object, _cache.Object);
    }

    private static ProductViewModel ToViewModel(Product p) => new()
    {
        Id = p.Id,
        Name = p.Name,
        Sku = p.Sku,
        Description = p.Description,
        Price = p.Price,
        QuantityInStock = p.QuantityInStock,
        SupplierName = p.SupplierName!,
        ExpiryDate = p.ExpiryDate,
        IsArchived = p.IsArchived,
        CreatedAt = p.CreatedAt,
        LastUpdatedAt = p.LastUpdatedAt ?? default
    };

    private static Product MakeProduct(
        string name,
        string sku,
        int quantity = 5,
        bool archived = false,
        DateTime? createdAt = null)
    {
        var product = Product.Create(name, sku, 10m, quantity);
        if (createdAt.HasValue)
            product.CreatedAt = createdAt.Value;
        if (archived)
            product.Archive();
        return product;
    }

    private void GivenProducts(params Product[] products) =>
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_NoFilter_ReturnsAllProducts()
    {
        GivenProducts(MakeProduct("Mouse", "SKU-001"), MakeProduct("Keyboard", "SKU-002"));

        var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_NoFilter_OrdersByCreatedAtDescending()
    {
        GivenProducts(
            MakeProduct("Oldest", "SKU-001", createdAt: new DateTime(2024, 1, 1)),
            MakeProduct("Newest", "SKU-002", createdAt: new DateTime(2026, 1, 1)),
            MakeProduct("Middle", "SKU-003", createdAt: new DateTime(2025, 1, 1)));

        var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        result.Select(p => p.Name).Should().ContainInOrder("Newest", "Middle", "Oldest");
    }

    [Fact]
    public async Task Handle_OnlyAvailable_ExcludesArchivedProducts()
    {
        GivenProducts(
            MakeProduct("Active", "SKU-001"),
            MakeProduct("Archived", "SKU-002", archived: true));

        var result = await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().Be("Active");
    }

    [Fact]
    public async Task Handle_OnlyAvailable_ExcludesOutOfStockProducts()
    {
        GivenProducts(
            MakeProduct("InStock", "SKU-001", quantity: 1),
            MakeProduct("OutOfStock", "SKU-002", quantity: 0));

        var result = await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().Be("InStock");
    }

    [Fact]
    public async Task Handle_CacheMiss_WritesResultToCache()
    {
        GivenProducts(MakeProduct("Mouse", "SKU-001"));

        await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        _cache.Verify(
            c => c.SetAsync(
                "products:all:False",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedValueWithoutQueryingRepository()
    {
        var cached = new[] { new ProductViewModel { Name = "FromCache", Sku = "SKU-999" } };
        _cache.Setup(c => c.GetAsync("products:all:False", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cached)));

        var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        result.Should().ContainSingle().Which.Name.Should().Be("FromCache");
        _productRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OnlyAvailableFlag_UsesSeparateCacheKey()
    {
        GivenProducts(MakeProduct("Mouse", "SKU-001"));

        await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

        _cache.Verify(c => c.GetAsync("products:all:True", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.GetAsync("products:all:False", It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyCollection()
    {
        GivenProducts();

        var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_AllProductsFilteredOut_ReturnsEmptyCollection()
    {
        GivenProducts(MakeProduct("Archived", "SKU-001", archived: true));

        var result = await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
    }

    [Fact]
    public async Task Handle_CacheReadThrows_PropagatesException()
    {
        // Infrastructure failure: Redis down on read. The handler has no try/catch,
        // so a cache outage takes the whole query down rather than degrading to the repository.
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis unavailable"));

        var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("redis unavailable");
    }

    [Fact]
    public async Task Handle_CacheWriteThrows_PropagatesException()
    {
        GivenProducts(MakeProduct("Mouse", "SKU-001"));
        _cache.Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis unavailable"));

        var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_CorruptCachePayload_ThrowsJsonException()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("{ not valid json"));

        var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    // ---------- edge cases ----------

    [Fact]
    public async Task Handle_MaxLengthProductName_IsPreservedIntact()
    {
        var longName = new string('X', 8000);
        GivenProducts(MakeProduct(longName, "SKU-001"));

        var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        result.Single().Name.Should().Be(longName).And.HaveLength(8000);
    }

    [Fact]
    public async Task Handle_MaxIntQuantity_IsPreservedWithoutOverflow()
    {
        GivenProducts(MakeProduct("Bulk", "SKU-001", quantity: int.MaxValue));

        var result = await _handler.Handle(new GetAllProductsQuery(OnlyAvailable: true), CancellationToken.None);

        result.Single().QuantityInStock.Should().Be(int.MaxValue);
    }

    [Fact]
    public async Task Handle_ProductsWithIdenticalCreatedAt_ReturnsAllOfThem()
    {
        var timestamp = new DateTime(2026, 1, 1);
        GivenProducts(
            MakeProduct("A", "SKU-001", createdAt: timestamp),
            MakeProduct("B", "SKU-002", createdAt: timestamp));

        var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_AbsentCacheEntry_FallsThroughToRepository()
    {
        GivenProducts(MakeProduct("Mouse", "SKU-001"));
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var result = await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        result.Should().ContainSingle();
        _productRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ZeroLengthCacheEntry_ThrowsInsteadOfFallingBack()
    {
        // DEFECT: GetStringAsync only treats a NULL byte[] as a miss. A zero-length entry
        // -- which a truncated or evicted-mid-write Redis value can produce -- decodes to
        // "" and blows up in the deserializer, turning a degraded cache into a hard 500
        // instead of a fall-through to the repository.
        GivenProducts(MakeProduct("Mouse", "SKU-001"));
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<byte>());

        var act = async () => await _handler.Handle(new GetAllProductsQuery(), CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
        _productRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
