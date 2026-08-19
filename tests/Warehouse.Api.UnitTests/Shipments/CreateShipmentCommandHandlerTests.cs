using FluentAssertions;
using Moq;
using Warehouse.Application.Common;
using Warehouse.Application.Shipments.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Shipments;

public class CreateShipmentCommandHandlerTests : ShipmentHandlerTestBase
{
    private CreateShipmentCommandHandler CreateHandler() =>
        new(ShipmentRepository.Object, SupplierRepository.Object, Mapper);

    private static CreateShipmentCommand Command(Guid supplierId, string? reference = "SHP-0001") =>
        new(supplierId, "12 Dock Road", "Beirut", "1107", "Lebanon", null, reference);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_ValidRequest_CreatesDraftShipment()
    {
        var supplier = MakeSupplier();
        GivenSupplier(supplier);

        var result = await CreateHandler().Handle(Command(supplier.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReferenceNumber.Should().Be("SHP-0001");
        result.Value.Status.Should().Be(nameof(ShipmentStatus.Draft));
        result.Value.SupplierId.Should().Be(supplier.Id);
        result.Value.SupplierName.Should().Be(supplier.Name);
        result.Value.Lines.Should().BeEmpty();
        result.Value.TotalUnits.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsExactlyOnce()
    {
        var supplier = MakeSupplier();
        GivenSupplier(supplier);

        await CreateHandler().Handle(Command(supplier.Id), CancellationToken.None);

        ShipmentRepository.Verify(r => r.AddAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MapsTheDestinationOntoTheViewModel()
    {
        var supplier = MakeSupplier();
        GivenSupplier(supplier);

        var result = await CreateHandler().Handle(Command(supplier.Id), CancellationToken.None);

        result.Value!.Destination.Line1.Should().Be("12 Dock Road");
        result.Value.Destination.City.Should().Be("Beirut");
        result.Value.Destination.PostalCode.Should().Be("1107");
        result.Value.Destination.Country.Should().Be("Lebanon");
    }

    [Fact]
    public async Task Handle_BlankReferenceNumber_GeneratesOne()
    {
        var supplier = MakeSupplier();
        GivenSupplier(supplier);

        var result = await CreateHandler().Handle(Command(supplier.Id, reference: null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.ReferenceNumber.Should().StartWith("SHP-").And.HaveLength(21);
    }

    [Fact]
    public async Task Handle_TrimsTheSuppliedReferenceNumber()
    {
        var supplier = MakeSupplier();
        GivenSupplier(supplier);

        var result = await CreateHandler().Handle(Command(supplier.Id, "  SHP-0001  "), CancellationToken.None);

        result.Value!.ReferenceNumber.Should().Be("SHP-0001");
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_UnknownSupplier_ReturnsNotFoundAndPersistsNothing()
    {
        SupplierRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Supplier?)null);

        var result = await CreateHandler().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        ShipmentRepository.Verify(r => r.AddAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_InactiveSupplier_ReturnsConflict()
    {
        var supplier = MakeSupplier(isActive: false);
        GivenSupplier(supplier);

        var result = await CreateHandler().Handle(Command(supplier.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("Shipments cannot be created for an inactive supplier.");
    }

    [Fact]
    public async Task Handle_DuplicateReferenceNumber_ReturnsConflict()
    {
        var supplier = MakeSupplier();
        GivenSupplier(supplier);
        ShipmentRepository
            .Setup(r => r.GetByReferenceNumberAsync("SHP-0001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeShipment(supplier));

        var result = await CreateHandler().Handle(Command(supplier.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("A shipment with reference 'SHP-0001' already exists.");
        ShipmentRepository.Verify(r => r.AddAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UniquenessCheckIsATargetedLookupNotAFullScan()
    {
        // Pins the fix for the pattern CreateProductCommandHandler uses, where the duplicate
        // check loads every row before rejecting one.
        var supplier = MakeSupplier();
        GivenSupplier(supplier);

        await CreateHandler().Handle(Command(supplier.Id), CancellationToken.None);

        ShipmentRepository.Verify(
            r => r.GetByReferenceNumberAsync("SHP-0001", It.IsAny<CancellationToken>()), Times.Once);
        ShipmentRepository.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_BlankDestinationCity_ReturnsValidationError()
    {
        var supplier = MakeSupplier();
        GivenSupplier(supplier);

        var command = new CreateShipmentCommand(supplier.Id, "12 Dock Road", "  ", "1107", "Lebanon", null, "SHP-0001");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("City is required.");
    }

    [Fact]
    public async Task Handle_PastExpectedDeliveryDate_ReturnsValidationError()
    {
        var supplier = MakeSupplier();
        GivenSupplier(supplier);

        var command = new CreateShipmentCommand(
            supplier.Id, "12 Dock Road", "Beirut", "1107", "Lebanon", DateTime.UtcNow.AddDays(-1), "SHP-0001");

        var result = await CreateHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("Expected delivery date cannot be in the past.");
    }
}
