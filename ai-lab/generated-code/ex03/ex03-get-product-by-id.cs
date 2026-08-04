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

public class GetProductByIdQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly GetProductByIdQueryHandler _handler;

    public GetProductByIdQueryHandlerTests()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        _mapper.Setup(m => m.Map<ProductViewModel>(It.IsAny<Product>()))
            .Returns((Product p) => new ProductViewModel
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
            });

        _handler = new GetProductByIdQueryHandler(_productRepository.Object, _mapper.Object, _cache.Object);
    }

    [Fact]
    public async Task Handle_ExistingProduct_ReturnsMappedViewModel()
    {
        var product = Product.Create("Wireless Mouse", "SKU-001", 19.99m, 5);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Wireless Mouse");
        result.Sku.Should().Be("SKU-001");
        result.Price.Should().Be(19.99m);
    }

    [Fact]
    public async Task Handle_CacheMiss_WritesResultUnderIdScopedKey()
    {
        var product = Product.Create("Wireless Mouse", "SKU-001", 19.99m, 5);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        _cache.Verify(
            c => c.SetAsync(
                $"products:{product.Id}",
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedValueWithoutQueryingRepository()
    {
        var id = Guid.NewGuid();
        var cached = new ProductViewModel { Id = id, Name = "FromCache", Sku = "SKU-999" };
        _cache.Setup(c => c.GetAsync($"products:{id}", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cached)));

        var result = await _handler.Handle(new GetProductByIdQuery(id), CancellationToken.None);

        result!.Name.Should().Be("FromCache");
        _productRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ArchivedProduct_IsStillReturned()
    {
        var product = Product.Create("Retired", "SKU-001", 10m, 5);
        product.Archive();
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.IsArchived.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MissingProduct_ReturnsNull()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MissingProduct_CachesTheNegativeResult()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        _cache.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.Is<byte[]>(b => Encoding.UTF8.GetString(b) == "null"),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNullWhenNotFound()
    {
        _productRepository.Setup(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await _handler.Handle(new GetProductByIdQuery(Guid.Empty), CancellationToken.None);

        result.Should().BeNull();
        _productRepository.Verify(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        _productRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var act = async () => await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
    }

    [Fact]
    public async Task Handle_CacheReadThrows_PropagatesException()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis unavailable"));

        var act = async () => await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("redis unavailable");
    }

    [Fact]
    public async Task Handle_CorruptCachePayload_ThrowsJsonException()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("<<<not json>>>"));

        var act = async () => await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<JsonException>();
    }

    [Fact]
    public async Task Handle_MaxLengthDescription_SurvivesSerializationRoundTrip()
    {
        var product = Product.Create("Mouse", "SKU-001", 10m, 5);
        product.Description = new string('D', 10_000);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result!.Description.Should().HaveLength(10_000);
    }

    [Fact]
    public async Task Handle_UnicodeAndControlCharactersInName_AreSerializedSafely()
    {
        var product = Product.Create("أœnأ¯cأ¸dأ© \"quoted\" \\ backslash", "SKU-001", 10m, 5);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result!.Name.Should().Be("أœnأ¯cأ¸dأ© \"quoted\" \\ backslash");
    }

    [Fact]
    public async Task Handle_ExpiryDateAtYearBoundary_IsReturnedUnshifted()
    {
        var product = Product.Create("Milk", "SKU-001", 10m, 5);
        product.ExpiryDate = DateTime.SpecifyKind(new DateTime(2025, 12, 31, 23, 59, 59), DateTimeKind.Unspecified);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var result = await _handler.Handle(new GetProductByIdQuery(product.Id), CancellationToken.None);

        result!.ExpiryDate.Should().Be(new DateTime(2025, 12, 31, 23, 59, 59));
        result.ExpiryDate!.Value.Year.Should().Be(2025);
    }

    [Fact]
    public async Task Handle_CachedPayloadOfLiteralNull_ReturnsNull()
    {
        _cache.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("null"));

        var result = await _handler.Handle(new GetProductByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
        _productRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
