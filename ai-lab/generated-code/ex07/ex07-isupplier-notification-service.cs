namespace Warehouse.Domain;

public interface ISupplierNotificationService
{
    Task NotifySupplierAsync(
        SupplierNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record SupplierNotification(
    Guid SupplierId,
    string SupplierName,
    string ContactEmail,
    Guid ShipmentId,
    string ShipmentReference,
    ShipmentStatus ShipmentStatus,
    string Message);
