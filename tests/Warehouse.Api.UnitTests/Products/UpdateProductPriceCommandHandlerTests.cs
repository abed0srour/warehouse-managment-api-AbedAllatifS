using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Warehouse.Application.Products.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Products;

public class UpdateProductPriceCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ILogger<UpdateProductPriceCommandHandler>> _logger = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly UpdateProductPriceCommandHandler _handler;

    public UpdateProductPriceCommandHandlerTests()
    {
        _handler = new UpdateProductPriceCommandHandler(_productRepository.Object, _logger.Object, _cache.Object);
    }

    private static Product MakeProduct(decimal price)
    {
        return Product.Create("Wireless Mouse", "SKU-001", price, 10);
    }

    [Fact]
    public async Task Handle_ValidPrice_Updates()
    {
        var product = MakeProduct(19.99m);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var result = await _handler.Handle(new UpdateProductPriceCommand(product.Id, 29.99m), CancellationToken.None);

        result!.Price.Should().Be(29.99m);
        _productRepository.Verify(r => r.UpdateAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public async Task Handle_InvalidPrice_Rejected(decimal invalidPrice)
    {
        var product = MakeProduct(19.99m);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);

        var act = async () => await _handler.Handle(new UpdateProductPriceCommand(product.Id, invalidPrice), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
        _productRepository.Verify(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
