using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Warehouse.Application.Common.Exceptions;
using Warehouse.Application.Products.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Products;

public class CreateProductCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly CreateProductCommandHandler _handler;

    public CreateProductCommandHandlerTests()
    {
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Product>());

        _handler = new CreateProductCommandHandler(_productRepository.Object, _mapper.Object, _cache.Object);
    }

    [Fact]
    public async Task Handle_ValidProduct_Succeeds()
    {
        var command = new CreateProductCommand("Wireless Mouse", "SKU-001", "A mouse", 19.99m, 50, "Acme", null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Name.Should().Be("Wireless Mouse");
        result.Sku.Should().Be("SKU-001");
        result.Price.Should().Be(19.99m);
        result.QuantityInStock.Should().Be(50);
        _productRepository.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateSku_ThrowsException()
    {
        var existing = Product.Create("Existing Product", "SKU-001", 10m, 5);
        _productRepository.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { existing });

        var command = new CreateProductCommand("New Product", "sku-001", "desc", 10m, 5, null, null);

        var act = async () => await _handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<BusinessRuleException>();
        _productRepository.Verify(r => r.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ValidProduct_CreatedDateAssigned()
    {
        var command = new CreateProductCommand("Wireless Mouse", "SKU-002", "A mouse", 19.99m, 50, null, null);
        var before = DateTime.UtcNow;

        var result = await _handler.Handle(command, CancellationToken.None);

        result.CreatedAt.Should().NotBe(default);
        result.CreatedAt.Should().BeOnOrAfter(DateTime.SpecifyKind(before.AddSeconds(-1), DateTimeKind.Unspecified));
    }

    [Fact]
    public async Task Handle_ValidProduct_GeneratedIdNotEmpty()
    {
        var command = new CreateProductCommand("Wireless Mouse", "SKU-003", "A mouse", 19.99m, 50, null, null);

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Id.Should().NotBeEmpty();
    }
}
