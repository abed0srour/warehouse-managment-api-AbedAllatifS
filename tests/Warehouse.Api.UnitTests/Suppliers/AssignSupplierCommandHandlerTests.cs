using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Moq;
using Warehouse.Application.Common.Exceptions;
using Warehouse.Application.Products.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Suppliers;

public class AssignSupplierCommandHandlerTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<ISupplierRepository> _supplierRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IDistributedCache> _cache = new();
    private readonly AssignSupplierCommandHandler _handler;

    public AssignSupplierCommandHandlerTests()
    {
        _handler = new AssignSupplierCommandHandler(_productRepository.Object, _supplierRepository.Object, _mapper.Object, _cache.Object);
    }

    private static Supplier MakeSupplier(bool isActive = true)
    {
        return new Supplier { Name = "Acme Corp", Country = "USA", ContactEmail = "a@acme.com", PhoneNumber = "555-0100", IsActive = isActive };
    }

    [Fact]
    public async Task Handle_ValidAssignment_AssignsSupplierToProduct()
    {
        var product = Product.Create("Wireless Mouse", "SKU-001", 19.99m, 10);
        var supplier = MakeSupplier();
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);

        var result = await _handler.Handle(new AssignSupplierCommand(product.Id, supplier.Id), CancellationToken.None);

        result.SupplierId.Should().Be(supplier.Id);
        result.SupplierName.Should().Be(supplier.Name);
        _productRepository.Verify(r => r.UpdateAsync(product, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ArchivedProduct_ThrowsException()
    {
        var product = Product.Create("Wireless Mouse", "SKU-001", 19.99m, 10);
        product.Archive();
        var supplier = MakeSupplier();
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);

        var act = async () => await _handler.Handle(new AssignSupplierCommand(product.Id, supplier.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _productRepository.Verify(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SupplierNotFound_ThrowsException()
    {
        var product = Product.Create("Wireless Mouse", "SKU-001", 19.99m, 10);
        _productRepository.Setup(r => r.GetByIdAsync(product.Id, It.IsAny<CancellationToken>())).ReturnsAsync(product);
        _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Supplier?)null);

        var act = async () => await _handler.Handle(new AssignSupplierCommand(product.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _productRepository.Verify(r => r.UpdateAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
