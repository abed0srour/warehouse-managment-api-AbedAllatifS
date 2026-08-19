using ShipmentStatus = Warehouse.Domain.ShipmentStatus;
// The domain and EF layers both define Shipment/ShipmentLine/ShipmentStatusChange; these
// factories build the EF ones.
using Shipment = Warehouse.Infrastructure.Data.EfModels.Shipment;
using ShipmentLine = Warehouse.Infrastructure.Data.EfModels.ShipmentLine;
using ShipmentStatusChange = Warehouse.Infrastructure.Data.EfModels.ShipmentStatusChange;

namespace Warehouse.Api.UnitTests.Queries;

/// <summary>
/// EF-model factories for the shipment read-side tests. These build persistence rows directly
/// rather than going through the domain aggregate, which is the point: the query handlers must
/// cope with whatever is actually in the tables, including states the aggregate would refuse to
/// produce.
/// </summary>
internal static class ShipmentSeed
{
    public static Shipment MakeShipment(
        Guid? id = null,
        string reference = "SHP-0001",
        ShipmentStatus status = ShipmentStatus.Draft,
        Guid? supplierId = null,
        string supplierName = "Acme Corp",
        string? trackingNumber = null,
        DateTime? expectedDeliveryDate = null,
        DateTime? dispatchedAt = null,
        DateTime? deliveredAt = null,
        DateTime? supplierNotifiedAt = null,
        DateTime? createdAt = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            ReferenceNumber = reference,
            SupplierId = supplierId ?? Guid.NewGuid(),
            SupplierName = supplierName,
            Status = status.ToString(),
            DestinationLine1 = "12 Dock Road",
            DestinationCity = "Beirut",
            DestinationPostalCode = "1107",
            DestinationCountry = "Lebanon",
            ExpectedDeliveryDate = expectedDeliveryDate,
            TrackingNumber = trackingNumber,
            DispatchedAt = dispatchedAt,
            DeliveredAt = deliveredAt,
            SupplierNotifiedAt = supplierNotifiedAt,
            CreatedAt = createdAt ?? new DateTime(2026, 1, 1),
            LastUpdatedAt = null
        };

    public static ShipmentLine MakeLine(
        Guid shipmentId,
        string productName = "Wireless Mouse",
        string sku = "SKU-001",
        int quantity = 1,
        Guid? productId = null) => new()
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            ProductId = productId ?? Guid.NewGuid(),
            ProductName = productName,
            Sku = sku,
            Quantity = quantity
        };

    public static ShipmentStatusChange MakeStatusChange(
        Guid shipmentId,
        ShipmentStatus from,
        ShipmentStatus to,
        DateTime occurredAt,
        string? note = null) => new()
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            FromStatus = from.ToString(),
            ToStatus = to.ToString(),
            Note = note,
            OccurredAt = occurredAt
        };
}
