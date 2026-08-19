using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Warehouse.Application;
using Warehouse.Application.Common;
using Warehouse.Application.Products.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Products;

public class AdjustStockCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly AdjustStockCommandHandler _handler;

    public AdjustStockCommandHandlerTests()
    {
        // The real profile rather than a stubbed IMapper: the handler returns a mapped view
        // model, and a hand-written stub would hide a broken mapping.
        var mapper = new MapperConfiguration(
            cfg => cfg.AddProfile<MappingProfile>(),
            NullLoggerFactory.Instance).CreateMapper();

        _handler = new AdjustStockCommandHandler(
            _productRepository.Object,
            mapper,
            _cache.Object,
            NullLogger<AdjustStockCommandHandler>.Instance);
    }

    private Product GivenProduct(int quantity = 10, bool archived = false)
    {
        var product = Product.Create("Wireless Mouse", "SKU-001", 19.99m, quantity);
        if (archived)
        {
            product.Archive();
        }

        _productRepository
            .Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        return product;
    }

    // ---------- positive ----------

    [Fact]
    public async Task Handle_Increase_RaisesTheStockLevel()
    {
        var product = GivenProduct(quantity: 10);

        var result = await _handler.Handle(new AdjustStockCommand(product.Id, 5, "Delivery received"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuantityInStock.Should().Be(15);
        result.Value.Id.Should().Be(product.Id);
        result.Value.Sku.Should().Be("SKU-001");
    }

    [Fact]
    public async Task Handle_Decrease_LowersTheStockLevel()
    {
        var product = GivenProduct(quantity: 10);

        var result = await _handler.Handle(new AdjustStockCommand(product.Id, -4, "Damaged"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuantityInStock.Should().Be(6);
    }

    [Fact]
    public async Task Handle_PersistsExactlyOnce()
    {
        var product = GivenProduct();

        await _handler.Handle(new AdjustStockCommand(product.Id, 1, null), CancellationToken.None);

        _productRepository.Verify(r => r.UpdateAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EvictsTheListAndSingleProductCacheEntries()
    {
        var product = GivenProduct();

        await _handler.Handle(new AdjustStockCommand(product.Id, 1, null), CancellationToken.None);

        _cache.Verify(c => c.RemoveAsync("products:all:True", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveAsync("products:all:False", It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveAsync($"products:{product.Id}", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MissingReason_IsAcceptedOnAnIncrease()
    {
        // The "reason required" rule belongs to the request contract on a decrease; the
        // command itself does not re-impose it.
        var product = GivenProduct();

        var result = await _handler.Handle(new AdjustStockCommand(product.Id, 5, null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_DownToExactlyZero_IsAllowed()
    {
        var product = GivenProduct(quantity: 10);

        var result = await _handler.Handle(new AdjustStockCommand(product.Id, -10, "Cleared"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.QuantityInStock.Should().Be(0);
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_UnknownProduct_ReturnsNotFoundAndPersistsNothing()
    {
        _productRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Product?)null);

        var result = await _handler.Handle(new AdjustStockCommand(Guid.NewGuid(), 5, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        _productRepository.Verify(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DecreaseBelowZero_ReturnsConflictAndLeavesTheLevelUntouched()
    {
        var product = GivenProduct(quantity: 10);

        var result = await _handler.Handle(new AdjustStockCommand(product.Id, -11, "Oops"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("Cannot decrease stock by 11: only 10 in stock.");
        product.QuantityInStock.Should().Be(10);
        _productRepository.Verify(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ArchivedProduct_ReturnsConflict()
    {
        var product = GivenProduct(archived: true);

        var result = await _handler.Handle(new AdjustStockCommand(product.Id, 5, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("Archived products cannot be updated.");
    }

    [Fact]
    public async Task Handle_ZeroChange_ReturnsValidationError()
    {
        var product = GivenProduct();

        var result = await _handler.Handle(new AdjustStockCommand(product.Id, 0, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("Stock adjustment cannot be zero.");
    }

    [Fact]
    public async Task Handle_FailedAdjustment_DoesNotEvictTheCache()
    {
        var product = GivenProduct(quantity: 1);

        await _handler.Handle(new AdjustStockCommand(product.Id, -5, "Oops"), CancellationToken.None);

        _cache.Verify(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OverflowingIncrease_ReturnsConflict()
    {
        var product = GivenProduct(quantity: 10);

        var result = await _handler.Handle(new AdjustStockCommand(product.Id, int.MaxValue, null), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("Resulting stock quantity is too large.");
    }
}
