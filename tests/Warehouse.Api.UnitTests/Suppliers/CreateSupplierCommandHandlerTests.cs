using AutoMapper;
using FluentAssertions;
using Moq;
using Warehouse.Application.Suppliers;
using Warehouse.Application.Suppliers.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Suppliers;

public class CreateSupplierCommandHandlerTests
{
    private readonly Mock<ISupplierRepository> _supplierRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly CreateSupplierCommandHandler _handler;

    public CreateSupplierCommandHandlerTests()
    {
        _mapper.Setup(m => m.Map<SupplierViewModel>(It.IsAny<Supplier>()))
            .Returns((Supplier s) => new SupplierViewModel
            {
                Id = s.Id,
                Name = s.Name,
                Country = s.Country,
                ContactEmail = s.ContactEmail,
                PhoneNumber = s.PhoneNumber,
                IsActive = s.IsActive
            });

        _handler = new CreateSupplierCommandHandler(_supplierRepository.Object, _mapper.Object);
    }

    [Fact]
    public async Task Handle_ValidSupplier_CreatesSupplier()
    {
        var command = new CreateSupplierCommand("Acme Corp", "USA", "contact@acme.com", "555-0100");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Acme Corp");
        result.Value.Country.Should().Be("USA");
        result.Value.ContactEmail.Should().Be("contact@acme.com");
        result.Value.PhoneNumber.Should().Be("555-0100");
        result.Value.IsActive.Should().BeTrue();
        _supplierRepository.Verify(r => r.AddAsync(It.IsAny<Supplier>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
