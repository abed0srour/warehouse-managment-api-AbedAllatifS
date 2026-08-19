namespace Warehouse.Application.Shipments.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Application.Shipments.Notifications;
using Warehouse.Domain;

/// <summary>
/// Moves a shipment along its lifecycle. <paramref name="NewStatus"/> is the enum *name*
/// ("Dispatched", "InTransit", "Delivered", "Cancelled") so the wire contract does not depend
/// on the ordering of <see cref="ShipmentStatus"/>.
/// </summary>
public record UpdateDeliveryStateCommand(
    Guid ShipmentId,
    string NewStatus,
    string? TrackingNumber = null,
    string? Note = null) : IRequest<Result<ShipmentViewModel>>;

public class UpdateDeliveryStateCommandHandler : IRequestHandler<UpdateDeliveryStateCommand, Result<ShipmentViewModel>>
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly IPublisher _publisher;
    private readonly IMapper _mapper;

    public UpdateDeliveryStateCommandHandler(
        IShipmentRepository shipmentRepository,
        IPublisher publisher,
        IMapper mapper)
    {
        _shipmentRepository = shipmentRepository;
        _publisher = publisher;
        _mapper = mapper;
    }

    public async Task<Result<ShipmentViewModel>> Handle(UpdateDeliveryStateCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ShipmentStatus>(request.NewStatus, ignoreCase: true, out var newStatus))
        {
            return Result.Failure<ShipmentViewModel>(
                ErrorType.Validation,
                $"'{request.NewStatus}' is not a valid shipment state. Valid states: {string.Join(", ", Enum.GetNames<ShipmentStatus>())}.");
        }

        var shipment = await _shipmentRepository.GetByIdAsync(request.ShipmentId, cancellationToken);
        if (shipment == null)
        {
            return Result.Failure<ShipmentViewModel>(
                ErrorType.NotFound, $"Shipment with ID {request.ShipmentId} was not found.");
        }

        var previousStatus = shipment.Status;

        try
        {
            shipment.UpdateDeliveryState(newStatus, request.TrackingNumber, request.Note);
        }
        catch (InvalidOperationException ex)
        {
            // Illegal transition, or a dispatch attempt on an empty shipment.
            return Result.Failure<ShipmentViewModel>(ErrorType.Conflict, ex.Message);
        }

        await _shipmentRepository.UpdateAsync(shipment, cancellationToken);

        // Published only after the new state is durable, so a subscriber can never react to
        // a transition that failed to save.
        await _publisher.Publish(
            new ShipmentDeliveryStateChanged(shipment.Id, shipment.ReferenceNumber, previousStatus, newStatus),
            cancellationToken);

        // Re-read so the response carries whatever the subscribers stamped on the shipment
        // (SupplierNotifiedAt, in particular) rather than a snapshot taken before they ran.
        var updated = await _shipmentRepository.GetByIdAsync(shipment.Id, cancellationToken) ?? shipment;

        return Result.Success(_mapper.Map<ShipmentViewModel>(updated));
    }
}
