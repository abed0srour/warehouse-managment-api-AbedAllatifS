using System;
using System.Collections.Generic;

namespace Warehouse.Infrastructure.Data.EfModels;

public partial class Shipment
{
    public Guid Id { get; set; }
    public string ReferenceNumber { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; }

    public string Status { get; set; }

    public string DestinationLine1 { get; set; }
    public string DestinationCity { get; set; }
    public string DestinationPostalCode { get; set; }
    public string DestinationCountry { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? SupplierNotifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }

    public virtual Supplier? Supplier { get; set; }
    public virtual ICollection<ShipmentLine> Lines { get; set; }
    public virtual ICollection<ShipmentStatusChange> StatusHistory { get; set; }
}

public partial class ShipmentLine
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; }
    public string Sku { get; set; }
    public int Quantity { get; set; }

    public virtual Shipment? Shipment { get; set; }
    public virtual Product? Product { get; set; }
}

public partial class ShipmentStatusChange
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public string FromStatus { get; set; }
    public string ToStatus { get; set; }
    public string? Note { get; set; }
    public DateTime OccurredAt { get; set; }

    public virtual Shipment? Shipment { get; set; }
}
