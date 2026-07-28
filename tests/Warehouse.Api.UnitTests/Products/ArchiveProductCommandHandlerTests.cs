using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Warehouse.Application.Products;
using Warehouse.Application.Products.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Products;

public class ArchiveProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly ArchiveProductCommandHandler _handler;

    public ArchiveProductCommandHandlerTests()
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

        _handler = new ArchiveProductCommandHandler(_productRepository.Object, _mapper.Object, _cache.Object);
    }

    [Fact]
    public async Task Handle_DeleteMarksArchivedOnly()
    {
        var product = Product.Create("Wireless Mouse", "SKU-001", 19.99m, 10);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _handler.Handle(new ArchiveProductCommand(product.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsArchived.Should().BeTrue();
        result.Value.Name.Should().Be("Wireless Mouse");
        result.Value.Sku.Should().Be("SKU-001");
        result.Value.Price.Should().Be(19.99m);
        result.Value.QuantityInStock.Should().Be(10);
        _productRepository.Verify(r => r.UpdateAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ArchivedItemRemainsInList()
    {
        var products = new List<Product> { Product.Create("Wireless Mouse", "SKU-001", 19.99m, 10) };
        var product = products[0];
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(products);

        await _handler.Handle(new ArchiveProductCommand(product.Id), CancellationToken.None);
        var allProducts = await _productRepository.Object.GetAllAsync(CancellationToken.None);

        allProducts.Should().ContainSingle(p => p.Id == product.Id && p.IsArchived);
    }
}
