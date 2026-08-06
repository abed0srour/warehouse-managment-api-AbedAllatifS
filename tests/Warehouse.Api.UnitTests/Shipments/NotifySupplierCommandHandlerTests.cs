using FluentAssertions;
using Moq;
using Warehouse.Application.Common;
using Warehouse.Application.Shipments.Commands;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Shipments;

public class NotifySupplierCommandHandlerTests : ShipmentHandlerTestBase
{
    private readonly Mock<ISupplierNotificationService> _notificationService = new();

    private NotifySupplierCommandHandler CreateHandler() =>
        new(ShipmentRepository.Object, SupplierRepository.Object, _notificationService.Object, Mapper);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_SendsTheNotificationToTheSuppliersContactEmail()
    {
        var supplier = MakeSupplier(contactEmail: "orders@acme.com");
        var shipment = MakeShipmentWithLine(supplier);
        GivenShipment(shipment);
        GivenSupplier(supplier);

        var result = await CreateHandler().Handle(
            new NotifySupplierCommand(shipment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notificationService.Verify(s => s.NotifySupplierAsync(
            It.Is<SupplierNotification>(n =>
                n.SupplierId == supplier.Id &&
                n.ContactEmail == "orders@acme.com" &&
                n.ShipmentId == shipment.Id &&
                n.ShipmentReference == shipment.ReferenceNumber &&
                n.ShipmentStatus == ShipmentStatus.Draft),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StampsSupplierNotifiedAtAndPersistsIt()
    {
        var supplier = MakeSupplier();
        var shipment = MakeShipmentWithLine(supplier);
        GivenShipment(shipment);
        GivenSupplier(supplier);

        var result = await CreateHandler().Handle(
            new NotifySupplierCommand(shipment.Id), CancellationToken.None);

        result.Value!.SupplierNotifiedAt.Should().NotBeNull();
        shipment.SupplierNotifiedAt.Should().NotBeNull();
        ShipmentRepository.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_CustomMessage_IsSentVerbatimAfterTrimming()
    {
        var supplier = MakeSupplier();
        var shipment = MakeShipmentWithLine(supplier);
        GivenShipment(shipment);
        GivenSupplier(supplier);

        await CreateHandler().Handle(
            new NotifySupplierCommand(shipment.Id, "  please expedite  "), CancellationToken.None);

        _notificationService.Verify(s => s.NotifySupplierAsync(
            It.Is<SupplierNotification>(n => n.Message == "please expedite"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoMessage_GeneratesOneFromTheCurrentStatus()
    {
        var supplier = MakeSupplier();
        var shipment = MakeShipmentWithLine(supplier);
        shipment.UpdateDeliveryState(ShipmentStatus.Dispatched, "TRK-99");
        GivenShipment(shipment);
        GivenSupplier(supplier);

        await CreateHandler().Handle(new NotifySupplierCommand(shipment.Id), CancellationToken.None);

        _notificationService.Verify(s => s.NotifySupplierAsync(
            It.Is<SupplierNotification>(n =>
                n.Message == $"Shipment {shipment.ReferenceNumber} has been dispatched (tracking TRK-99)."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DispatchedWithoutTracking_OmitsTheTrackingClause()
    {
        var supplier = MakeSupplier();
        var shipment = MakeShipmentWithLine(supplier);
        shipment.UpdateDeliveryState(ShipmentStatus.Dispatched);
        GivenShipment(shipment);
        GivenSupplier(supplier);

        await CreateHandler().Handle(new NotifySupplierCommand(shipment.Id), CancellationToken.None);

        _notificationService.Verify(s => s.NotifySupplierAsync(
            It.Is<SupplierNotification>(n => n.Message == $"Shipment {shipment.ReferenceNumber} has been dispatched."),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_InactiveSupplier_IsStillNotified()
    {
        // Deactivating a supplier stops new shipments; it does not orphan the ones already moving.
        var supplier = MakeSupplier(isActive: false);
        var shipment = MakeShipmentWithLine(MakeSupplier());
        GivenShipment(shipment);
        SupplierRepository
            .Setup(r => r.GetByIdAsync(shipment.SupplierId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(supplier);

        var result = await CreateHandler().Handle(new NotifySupplierCommand(shipment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _notificationService.Verify(
            s => s.NotifySupplierAsync(It.IsAny<SupplierNotification>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_UnknownShipment_ReturnsNotFoundAndSendsNothing()
    {
        ShipmentRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var result = await CreateHandler().Handle(new NotifySupplierCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
        _notificationService.Verify(
            s => s.NotifySupplierAsync(It.IsAny<SupplierNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MissingSupplierRow_ReturnsNotFound()
    {
        var shipment = MakeShipmentWithLine();
        GivenShipment(shipment);
        SupplierRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Supplier?)null);

        var result = await CreateHandler().Handle(new NotifySupplierCommand(shipment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_SupplierWithNoContactEmail_ReturnsValidationError()
    {
        var supplier = MakeSupplier(contactEmail: "  ");
        var shipment = MakeShipmentWithLine(supplier);
        GivenShipment(shipment);
        GivenSupplier(supplier);

        var result = await CreateHandler().Handle(new NotifySupplierCommand(shipment.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Be("Supplier 'Acme Corp' has no contact email on file.");
        _notificationService.Verify(
            s => s.NotifySupplierAsync(It.IsAny<SupplierNotification>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheChannelThrows_TheShipmentIsNotStamped()
    {
        var supplier = MakeSupplier();
        var shipment = MakeShipmentWithLine(supplier);
        GivenShipment(shipment);
        GivenSupplier(supplier);
        _notificationService
            .Setup(s => s.NotifySupplierAsync(It.IsAny<SupplierNotification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("smtp down"));

        var act = async () => await CreateHandler().Handle(new NotifySupplierCommand(shipment.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        shipment.SupplierNotifiedAt.Should().BeNull();
        ShipmentRepository.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
