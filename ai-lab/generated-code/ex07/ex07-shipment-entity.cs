namespace Warehouse.Domain;

using System;
using System.Collections.Generic;

public class Shipment
{
    private readonly List<ShipmentLine> _lines = new();
    private readonly List<ShipmentStatusChange> _statusHistory = new();

    public Guid Id { get; private set; }
    public string ReferenceNumber { get; private set; }
    public Guid SupplierId { get; private set; }
    public string SupplierName { get; private set; }
    public ShipmentStatus Status { get; private set; }
    public Address Destination { get; private set; }
    public DateTime? ExpectedDeliveryDate { get; private set; }
    public string? TrackingNumber { get; private set; }
    public DateTime? DispatchedAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? SupplierNotifiedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastUpdatedAt { get; private set; }

    public IReadOnlyCollection<ShipmentLine> Lines => _lines.AsReadOnly();
    public IReadOnlyCollection<ShipmentStatusChange> StatusHistory => _statusHistory.AsReadOnly();

    public int TotalUnits => throw new NotImplementedException();
    public bool IsClosed => throw new NotImplementedException();

    private Shipment()
    {
    }

    public static Shipment Create(
        string referenceNumber,
        Supplier supplier,
        Address destination,
        DateTime? expectedDeliveryDate) => throw new NotImplementedException();

    public static Shipment Reconstruct(
        Guid id,
        string referenceNumber,
        Guid supplierId,
        string supplierName,
        ShipmentStatus status,
        Address destination,
        DateTime? expectedDeliveryDate,
        string? trackingNumber,
        DateTime? dispatchedAt,
        DateTime? deliveredAt,
        DateTime? supplierNotifiedAt,
        DateTime createdAt,
        DateTime? lastUpdatedAt,
        IEnumerable<ShipmentLine> lines,
        IEnumerable<ShipmentStatusChange> statusHistory) => throw new NotImplementedException();

    public void AssignProduct(Product product, int quantity) => throw new NotImplementedException();

    public void RemoveProduct(Guid productId) => throw new NotImplementedException();

    public void UpdateDeliveryState(
        ShipmentStatus newStatus,
        string? trackingNumber = null,
        string? note = null) => throw new NotImplementedException();

    public void RecordSupplierNotification(DateTime sentAt) => throw new NotImplementedException();
}
