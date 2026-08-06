namespace Warehouse.Infrastructure.Notifications;

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Warehouse.Domain;

/// <summary>
/// Writes supplier notifications to the application log.
///
/// This project has no mail or message-bus dependency wired up, so rather than pull one in as a
/// side effect of the shipment feature, the outbound channel is kept behind
/// <see cref="ISupplierNotificationService"/> and backed by the same Serilog sink the expiry job
/// already uses. Swapping in SMTP or a webhook is a registration change in Program.cs, nothing more.
/// </summary>
public class LoggingSupplierNotificationService : ISupplierNotificationService
{
    private readonly ILogger<LoggingSupplierNotificationService> _logger;

    public LoggingSupplierNotificationService(ILogger<LoggingSupplierNotificationService> logger)
    {
        _logger = logger;
    }

    public Task NotifySupplierAsync(SupplierNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Supplier notification for shipment {ShipmentReference} ({ShipmentId}) sent to {SupplierName} <{ContactEmail}>: status {ShipmentStatus} -- {Message}",
            notification.ShipmentReference,
            notification.ShipmentId,
            notification.SupplierName,
            notification.ContactEmail,
            notification.ShipmentStatus,
            notification.Message);

        return Task.CompletedTask;
    }
}
