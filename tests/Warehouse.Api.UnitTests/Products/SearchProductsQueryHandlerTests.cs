using AutoMapper;
using FluentAssertions;
using Moq;
using Warehouse.Application.Products;
using Warehouse.Application.Products.Queries;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Products;

public class SearchProductsQueryHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly SearchProductsQueryHandler _handler;

    public SearchProductsQueryHandlerTests()
    {
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

        _handler = new SearchProductsQueryHandler(_productRepository.Object, _mapper.Object);
    }

    private static Product MakeProduct(string name, string sku, string? supplierName)
    {
        var product = Product.Create(name, sku, 10m, 5);
        product.SupplierName = supplierName;
        return product;
    }

    [Fact]
    public async Task Handle_SearchByName_ReturnsMatches()
    {
        var products = new[]
        {
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("Wireless Keyboard", "SKU-002", "Acme"),
            MakeProduct("USB Cable", "SKU-003", "Globex")
        };
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

        var result = await _handler.Handle(new SearchProductsQuery("Wireless", null), CancellationToken.None);

        result.Should().HaveCount(2);
        result.Select(p => p.Sku).Should().BeEquivalentTo("SKU-001", "SKU-002");
    }

    [Fact]
    public async Task Handle_SearchBySupplier_ReturnsMatches()
    {
        var products = new[]
        {
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("Wireless Keyboard", "SKU-002", "Acme"),
            MakeProduct("USB Cable", "SKU-003", "Globex")
        };
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

        var result = await _handler.Handle(new SearchProductsQuery(null, "Globex"), CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Sku.Should().Be("SKU-003");
    }

    [Fact]
    public async Task Handle_SearchByBothFilters_ReturnsIntersection()
    {
        var products = new[]
        {
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("Wireless Keyboard", "SKU-002", "Globex"),
            MakeProduct("USB Cable", "SKU-003", "Acme")
        };
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

        var result = await _handler.Handle(new SearchProductsQuery("Wireless", "Acme"), CancellationToken.None);

        result.Should().ContainSingle();
        result.Single().Sku.Should().Be("SKU-001");
    }

    [Fact]
    public async Task Handle_EmptyFilters_ReturnsAllProducts()
    {
        var products = new[]
        {
            MakeProduct("Wireless Mouse", "SKU-001", "Acme"),
            MakeProduct("USB Cable", "SKU-002", "Globex")
        };
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

        var result = await _handler.Handle(new SearchProductsQuery(null, null), CancellationToken.None);

        result.Should().HaveCount(2);
    }
}
