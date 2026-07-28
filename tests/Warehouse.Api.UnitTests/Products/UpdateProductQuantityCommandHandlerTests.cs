using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Warehouse.Application.Products.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Products;

public class UpdateProductQuantityCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly UpdateProductQuantityCommandHandler _handler;

    public UpdateProductQuantityCommandHandlerTests()
    {
        _handler = new UpdateProductQuantityCommandHandler(_productRepository.Object, _mapper.Object, _cache.Object);
    }

    private static Product MakeProduct(int quantity)
    {
        return Product.Create("Wireless Mouse", "SKU-001", 19.99m, quantity);
    }

    [Fact]
    public async Task Handle_ValidQuantity_UpdatesStock()
    {
        var product = MakeProduct(20);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _handler.Handle(new UpdateProductQuantityCommand(product.Id, 35), CancellationToken.None);

        result!.QuantityInStock.Should().Be(35);
        _productRepository.Verify(r => r.UpdateAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    [InlineData(-100)]
    public async Task Handle_NegativeQuantity_Rejected(int negativeQuantity)
    {
        var product = MakeProduct(20);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var act = async () => await _handler.Handle(new UpdateProductQuantityCommand(product.Id, negativeQuantity), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        _productRepository.Verify(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidQuantity_LastUpdatedChanges()
    {
        var product = MakeProduct(20);
        product.LastUpdatedAt.Should().BeNull();
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _handler.Handle(new UpdateProductQuantityCommand(product.Id, 35), CancellationToken.None);

        result!.LastUpdatedAt.Should().NotBeNull();
    }
}
