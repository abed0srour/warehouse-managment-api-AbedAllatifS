namespace Warehouse.Application.Shipments.Commands;

using AutoMapper;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Common;
using Warehouse.Domain;

/// <summary>
/// Sends the supplier an update about one of its shipments and stamps the shipment with the
/// time it went out. Normally raised automatically by the delivery-state change subscriber;
/// exposed as a command so an operator can also re-send on demand with a custom message.
/// </summary>
public record NotifySupplierCommand(Guid ShipmentId, string? Message = null) : IRequest<Result<ShipmentViewModel>>;

public class NotifySupplierCommandHandler : IRequestHandler<NotifySupplierCommand, Result<ShipmentViewModel>>
{
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ISupplierNotificationService _notificationService;
    private readonly IMapper _mapper;

    public NotifySupplierCommandHandler(
        IShipmentRepository shipmentRepository,
        ISupplierRepository supplierRepository,
        ISupplierNotificationService notificationService,
        IMapper mapper)
    {
        _shipmentRepository = shipmentRepository;
        _supplierRepository = supplierRepository;
        _notificationService = notificationService;
        _mapper = mapper;
    }

    public async Task<Result<ShipmentViewModel>> Handle(NotifySupplierCommand request, CancellationToken cancellationToken)
    {
        var shipment = await _shipmentRepository.GetByIdAsync(request.ShipmentId, cancellationToken);
        if (shipment == null)
        {
            return Result.Failure<ShipmentViewModel>(
                ErrorType.NotFound, $"Shipment with ID {request.ShipmentId} was not found.");
        }

        var supplier = await _supplierRepository.GetByIdAsync(shipment.SupplierId, cancellationToken);
        if (supplier == null)
        {
            return Result.Failure<ShipmentViewModel>(
                ErrorType.NotFound, $"Supplier with ID {shipment.SupplierId} was not found.");
        }

        if (string.IsNullOrWhiteSpace(supplier.ContactEmail))
        {
            return Result.Failure<ShipmentViewModel>(
                ErrorType.Validation, $"Supplier '{supplier.Name}' has no contact email on file.");
        }

        var message = string.IsNullOrWhiteSpace(request.Message)
            ? DefaultMessage(shipment)
            : request.Message.Trim();

        await _notificationService.NotifySupplierAsync(
            new SupplierNotification(
                supplier.Id,
                supplier.Name,
                supplier.ContactEmail,
                shipment.Id,
                shipment.ReferenceNumber,
                shipment.Status,
                message),
            cancellationToken);

        // Stamped only after the send succeeded -- a throwing channel leaves no false record.
        shipment.RecordSupplierNotification(DateTime.UtcNow);
        await _shipmentRepository.UpdateAsync(shipment, cancellationToken);

        return Result.Success(_mapper.Map<ShipmentViewModel>(shipment));
    }

    private static string DefaultMessage(Shipment shipment) => shipment.Status switch
    {
        ShipmentStatus.Dispatched => $"Shipment {shipment.ReferenceNumber} has been dispatched" +
            (string.IsNullOrWhiteSpace(shipment.TrackingNumber) ? "." : $" (tracking {shipment.TrackingNumber})."),
        ShipmentStatus.InTransit => $"Shipment {shipment.ReferenceNumber} is in transit.",
        ShipmentStatus.Delivered => $"Shipment {shipment.ReferenceNumber} was delivered on {shipment.DeliveredAt:yyyy-MM-dd}.",
        ShipmentStatus.Cancelled => $"Shipment {shipment.ReferenceNumber} has been cancelled.",
        _ => $"Shipment {shipment.ReferenceNumber} is being prepared."
    };
}
