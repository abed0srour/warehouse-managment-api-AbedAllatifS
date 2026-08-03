using FluentAssertions;
using Moq;
using Warehouse.Application.Products.Queries;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Products;

public class GetExpiringSoonProductsQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly GetExpiringSoonProductsQueryHandler _handler;

    public GetExpiringSoonProductsQueryHandlerTests()
    {
        _handler = new GetExpiringSoonProductsQueryHandler(_productRepository.Object);
    }

    private static Product MakeProduct(string sku, int? expiresInDays, int quantity = 10)
    {
        var product = Product.Create($"Product {sku}", sku, 9.99m, quantity);
        if (expiresInDays.HasValue)
        {
            product.ExpiryDate = DateTime.UtcNow.Date.AddDays(expiresInDays.Value);
        }
        return product;
    }

    [Fact]
    public async Task Handle_ProductExpiringWithinRange_IsIncluded()
    {
        var product = MakeProduct("SKU-001", expiresInDays: 5);
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { product });

        var result = await _handler.Handle(new GetExpiringSoonProductsQuery(30), CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Sku.Should().Be("SKU-001");
        result.Single().DaysUntilExpiry.Should().Be(5);
    }

    [Fact]
    public async Task Handle_ProductExpiringOutsideRange_IsExcluded()
    {
        var product = MakeProduct("SKU-002", expiresInDays: 45);
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { product });

        var result = await _handler.Handle(new GetExpiringSoonProductsQuery(30), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ProductAlreadyExpired_IsExcluded()
    {
        var product = MakeProduct("SKU-003", expiresInDays: -1);
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { product });

        var result = await _handler.Handle(new GetExpiringSoonProductsQuery(30), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ProductWithNoExpiryDate_IsExcluded()
    {
        var product = MakeProduct("SKU-004", expiresInDays: null);
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { product });

        var result = await _handler.Handle(new GetExpiringSoonProductsQuery(30), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ArchivedProduct_IsExcluded()
    {
        var product = MakeProduct("SKU-005", expiresInDays: 5);
        product.Archive();
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { product });

        var result = await _handler.Handle(new GetExpiringSoonProductsQuery(30), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MultipleMatches_OrderedBySoonestExpiryFirst()
    {
        var products = new[]
        {
            MakeProduct("SKU-LATE", expiresInDays: 20),
            MakeProduct("SKU-SOON", expiresInDays: 2),
            MakeProduct("SKU-MID", expiresInDays: 10)
        };
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

        var result = await _handler.Handle(new GetExpiringSoonProductsQuery(30), CancellationToken.None);

        result.Select(p => p.Sku).Should().ContainInOrder("SKU-SOON", "SKU-MID", "SKU-LATE");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    public async Task Handle_NegativeWithinDays_ThrowsException(int withinDays)
    {
        var act = async () => await _handler.Handle(new GetExpiringSoonProductsQuery(withinDays), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
