using AutoMapper;
using FluentAssertions;
using Moq;
using Warehouse.Application.Suppliers;
using Warehouse.Application.Suppliers.Queries;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Suppliers;

public class GetSupplierByIdQueryHandlerTests
{
    private readonly Mock<ISupplierRepository> _supplierRepository = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly GetSupplierByIdQueryHandler _handler;

    public GetSupplierByIdQueryHandlerTests()
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

        _handler = new GetSupplierByIdQueryHandler(_supplierRepository.Object, _mapper.Object);
    }

    // ---------- positive ----------

    [Fact]
    public async Task Handle_ExistingSupplier_ReturnsMappedViewModel()
    {
        var supplier = new Supplier { Name = "Acme", Country = "US", IsActive = true };
        _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var result = await _handler.Handle(new GetSupplierByIdQuery(supplier.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(supplier.Id);
        result.Name.Should().Be("Acme");
        result.Country.Should().Be("US");
    }

    [Fact]
    public async Task Handle_InactiveSupplier_IsStillReturned()
    {
        var supplier = new Supplier { Name = "Retired", IsActive = false };
        _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var result = await _handler.Handle(new GetSupplierByIdQuery(supplier.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_QueriesRepositoryWithRequestedId()
    {
        var id = Guid.NewGuid();
        _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Supplier?)null);

        await _handler.Handle(new GetSupplierByIdQuery(id), CancellationToken.None);

        _supplierRepository.Verify(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_MissingSupplier_ReturnsNull()
    {
        _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Supplier?)null);

        var result = await _handler.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MissingSupplier_DoesNotInvokeMapper()
    {
        _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Supplier?)null);

        await _handler.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), CancellationToken.None);

        _mapper.Verify(m => m.Map<SupplierViewModel>(It.IsAny<Supplier>()), Times.Never);
    }

    [Fact]
    public async Task Handle_EmptyGuid_ReturnsNullWhenNotFound()
    {
        _supplierRepository.Setup(r => r.GetByIdAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Supplier?)null);

        var result = await _handler.Handle(new GetSupplierByIdQuery(Guid.Empty), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RepositoryThrows_PropagatesException()
    {
        _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));

        var act = async () => await _handler.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("database unavailable");
    }

    [Fact]
    public async Task Handle_CancelledToken_PropagatesOperationCanceled()
    {
        _supplierRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await _handler.Handle(new GetSupplierByIdQuery(Guid.NewGuid()), new CancellationToken(true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---------- edge cases ----------

    [Fact]
    public async Task Handle_MaxLengthContactEmail_IsPreservedIntact()
    {
        var longEmail = new string('e', 300) + "@example.test";
        var supplier = new Supplier { Name = "Acme", ContactEmail = longEmail };
        _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var result = await _handler.Handle(new GetSupplierByIdQuery(supplier.Id), CancellationToken.None);

        result!.ContactEmail.Should().Be(longEmail);
    }

    [Fact]
    public async Task Handle_DefaultStringFields_MapToEmptyNotNull()
    {
        var supplier = new Supplier { Name = "Acme" };
        _supplierRepository.Setup(r => r.GetByIdAsync(supplier.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var result = await _handler.Handle(new GetSupplierByIdQuery(supplier.Id), CancellationToken.None);

        result!.Country.Should().BeEmpty();
        result.ContactEmail.Should().BeEmpty();
        result.PhoneNumber.Should().BeEmpty();
    }
}
