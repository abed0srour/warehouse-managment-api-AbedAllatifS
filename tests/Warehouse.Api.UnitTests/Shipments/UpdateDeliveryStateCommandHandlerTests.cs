using FluentAssertions;
using MediatR;
using Moq;
using Warehouse.Application.Common;
using Warehouse.Application.Shipments.Commands;
using Warehouse.Application.Shipments.Notifications;
using Warehouse.Domain;

namespace Warehouse.Api.UnitTests.Shipments;

public class UpdateDeliveryStateCommandHandlerTests : ShipmentHandlerTestBase
{
    private readonly Mock<IPublisher> _publisher = new();

    private UpdateDeliveryStateCommandHandler CreateHandler() =>
        new(ShipmentRepository.Object, _publisher.Object, Mapper);

    // ---------- positive ----------

    [Fact]
    public async Task Handle_DispatchingADraftWithLines_MovesItToDispatched()
    {
        var shipment = MakeShipmentWithLine();
        GivenShipment(shipment);

        var result = await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(shipment.Id, "Dispatched", "TRK-99", "left the dock"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(nameof(ShipmentStatus.Dispatched));
        result.Value.TrackingNumber.Should().Be("TRK-99");
        result.Value.DispatchedAt.Should().NotBeNull();
        ShipmentRepository.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_StatusNameIsCaseInsensitive()
    {
        var shipment = MakeShipmentWithLine();
        GivenShipment(shipment);

        var result = await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(shipment.Id, "dispatched"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(nameof(ShipmentStatus.Dispatched));
    }

    [Fact]
    public async Task Handle_PublishesTheStateChangeWithBothStates()
    {
        var shipment = MakeShipmentWithLine();
        GivenShipment(shipment);

        await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(shipment.Id, "Dispatched"), CancellationToken.None);

        _publisher.Verify(p => p.Publish(
            It.Is<ShipmentDeliveryStateChanged>(n =>
                n.ShipmentId == shipment.Id &&
                n.ReferenceNumber == shipment.ReferenceNumber &&
                n.PreviousStatus == ShipmentStatus.Draft &&
                n.NewStatus == ShipmentStatus.Dispatched),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PublishesOnlyAfterThePersistCall()
    {
        // Ordering matters: a subscriber must never react to a transition that failed to save.
        var shipment = MakeShipmentWithLine();
        GivenShipment(shipment);

        var sequence = new List<string>();
        ShipmentRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("update"))
            .Returns(Task.CompletedTask);
        _publisher
            .Setup(p => p.Publish(It.IsAny<ShipmentDeliveryStateChanged>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("publish"))
            .Returns(Task.CompletedTask);

        await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(shipment.Id, "Dispatched"), CancellationToken.None);

        sequence.Should().ContainInOrder("update", "publish");
    }

    [Fact]
    public async Task Handle_DeliveringAnInTransitShipment_StampsDeliveredAt()
    {
        var shipment = MakeShipmentWithLine();
        shipment.UpdateDeliveryState(ShipmentStatus.Dispatched);
        shipment.UpdateDeliveryState(ShipmentStatus.InTransit);
        GivenShipment(shipment);

        var result = await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(shipment.Id, "Delivered"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.DeliveredAt.Should().NotBeNull();
    }

    // ---------- negative ----------

    [Fact]
    public async Task Handle_UnparseableStatus_ReturnsValidationErrorAndNeverLoadsTheShipment()
    {
        var result = await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(Guid.NewGuid(), "Teleported"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Message.Should().Contain("Dispatched").And.Contain("Delivered");
        ShipmentRepository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_UnknownShipment_ReturnsNotFound()
    {
        ShipmentRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Shipment?)null);

        var result = await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(Guid.NewGuid(), "Dispatched"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task Handle_IllegalTransition_ReturnsConflictAndPublishesNothing()
    {
        var shipment = MakeShipmentWithLine();
        GivenShipment(shipment);

        var result = await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(shipment.Id, "Delivered"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("Cannot move a shipment from 'Draft' to 'Delivered'.");
        ShipmentRepository.Verify(r => r.UpdateAsync(It.IsAny<Shipment>(), It.IsAny<CancellationToken>()), Times.Never);
        _publisher.Verify(
            p => p.Publish(It.IsAny<ShipmentDeliveryStateChanged>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DispatchingAnEmptyShipment_ReturnsConflict()
    {
        var shipment = MakeShipment();
        GivenShipment(shipment);

        var result = await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(shipment.Id, "Dispatched"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("A shipment cannot be dispatched with no products assigned.");
    }

    [Fact]
    public async Task Handle_RepeatingTheCurrentState_ReturnsConflict()
    {
        var shipment = MakeShipmentWithLine();
        GivenShipment(shipment);

        var result = await CreateHandler().Handle(
            new UpdateDeliveryStateCommand(shipment.Id, "Draft"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Conflict);
        result.Error.Message.Should().Be("Shipment is already in state 'Draft'.");
    }
}
