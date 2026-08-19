using System;
using System.Collections.Generic;

namespace Warehouse.Infrastructure.Data.EfModels;

public partial class Shipment
{
    public Guid Id { get; set; }

    public string ReferenceNumber { get; set; } = null!;

    public Guid SupplierId { get; set; }

    public string SupplierName { get; set; } = null!;

    /// <summary>
    /// Persisted as text rather than an int so the column stays readable in the database and
    /// a reordered enum cannot silently repoint existing rows.
    /// </summary>
    public string Status { get; set; } = null!;

    public string DestinationLine1 { get; set; } = null!;

    public string DestinationCity { get; set; } = null!;

    public string DestinationPostalCode { get; set; } = null!;

    public string DestinationCountry { get; set; } = null!;

    public DateTime? ExpectedDeliveryDate { get; set; }

    public string? TrackingNumber { get; set; }

    public DateTime? DispatchedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? SupplierNotifiedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastUpdatedAt { get; set; }

    public virtual Supplier? Supplier { get; set; }

    public virtual ICollection<ShipmentLine> Lines { get; set; } = new List<ShipmentLine>();

    public virtual ICollection<ShipmentStatusChange> StatusHistory { get; set; } = new List<ShipmentStatusChange>();
}
