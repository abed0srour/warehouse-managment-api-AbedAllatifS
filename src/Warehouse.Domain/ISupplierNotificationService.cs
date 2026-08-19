namespace Warehouse.Domain;

/// <summary>
/// Outbound channel for telling a supplier something about one of its shipments.
/// Declared here next to <see cref="IFileStorageService"/> so the application layer can depend
/// on the capability without depending on how it is delivered (log, email, webhook).
/// </summary>
public interface ISupplierNotificationService
{
    Task NotifySupplierAsync(
        SupplierNotification notification,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// What gets sent. A value object so the message is assembled and validated once,
/// by the handler that has the shipment in hand.
/// </summary>
public sealed record SupplierNotification(
    Guid SupplierId,
    string SupplierName,
    string ContactEmail,
    Guid ShipmentId,
    string ShipmentReference,
    ShipmentStatus ShipmentStatus,
    string Message);
