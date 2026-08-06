namespace Warehouse.Application.Shipments.Notifications;

using MediatR;
using Microsoft.Extensions.Logging;
using System.Threading;
using System.Threading.Tasks;
using Warehouse.Application.Shipments.Commands;
using Warehouse.Domain;

/// <summary>
/// Turns a persisted delivery-state change into a supplier notification.
///
/// Why a subscriber and not a call inside <see cref="UpdateDeliveryStateCommandHandler"/>:
/// notifying is a consequence of the transition, not part of it. Keeping it here means the
/// state machine has one job, the notification rule (which states are worth an email) lives in
/// one readable place, and a second consequence -- an audit feed, a customer webhook -- is a new
/// subscriber rather than an edit to the command handler.
///
/// Draft is the only state that produces no notification: nothing has happened yet that the
/// supplier needs to hear about, and it is unreachable as a target anyway.
/// </summary>
public class NotifySupplierOnDeliveryStateChanged : INotificationHandler<ShipmentDeliveryStateChanged>
{
    private readonly ISender _sender;
    private readonly ILogger<NotifySupplierOnDeliveryStateChanged> _logger;

    public NotifySupplierOnDeliveryStateChanged(ISender sender, ILogger<NotifySupplierOnDeliveryStateChanged> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task Handle(ShipmentDeliveryStateChanged notification, CancellationToken cancellationToken)
    {
        if (notification.NewStatus == ShipmentStatus.Draft)
        {
            return;
        }

        var result = await _sender.Send(new NotifySupplierCommand(notification.ShipmentId), cancellationToken);

        // The transition itself is already committed. A failed notification is worth a log line,
        // not a failed request -- the operator can re-send through the notify endpoint.
        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Could not notify the supplier about shipment {ShipmentReference} moving to {NewStatus}: {Reason}",
                notification.ReferenceNumber,
                notification.NewStatus,
                result.Error!.Message);
        }
    }
}
