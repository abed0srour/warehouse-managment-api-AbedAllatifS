namespace Warehouse.Application.Shipments.Notifications;

using MediatR;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Domain;

public record ShipmentDeliveryStateChanged(
    Guid ShipmentId,
    string ReferenceNumber,
    ShipmentStatus PreviousStatus,
    ShipmentStatus NewStatus) : INotification;

public class NotifySupplierOnDeliveryStateChanged : INotificationHandler<ShipmentDeliveryStateChanged>
{
    private readonly ISender _sender;
    private readonly ILogger<NotifySupplierOnDeliveryStateChanged> _logger;

    public NotifySupplierOnDeliveryStateChanged(
        ISender sender,
        ILogger<NotifySupplierOnDeliveryStateChanged> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public Task Handle(
        ShipmentDeliveryStateChanged notification,
        CancellationToken cancellationToken) => throw new NotImplementedException();
}
