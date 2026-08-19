using FluentAssertions;
using Moq;
using Warehouse.Application.Suppliers.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Suppliers;

public class DeactivateSupplierCommandHandlerTests
{
    private readonly Mock<ISupplierRepository> _supplierRepository = new();
    private readonly DeactivateSupplierCommandHandler _handler;

    public DeactivateSupplierCommandHandlerTests()
    {
        _handler = new DeactivateSupplierCommandHandler(_supplierRepository.Object);
    }

    [Fact]
    public async Task Handle_ExistingSupplier_DeactivatesSupplier()
    {
        var supplier = new Supplier { Name = "Acme Corp", Country = "USA", ContactEmail = "a@acme.com", PhoneNumber = "555-0100", IsActive = true };
        _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>())).ReturnsAsync(supplier);

        var result = await _handler.Handle(new DeactivateSupplierCommand(supplier.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        supplier.IsActive.Should().BeFalse();
        _supplierRepository.Verify(r => r.UpdateAsync(supplier, It.IsAny<CancellationToken>()), Times.Once);
    }
}
