using System;

namespace Warehouse.Infrastructure.Data.EfModels;

public partial class ShipmentStatusChange
{
    public Guid Id { get; set; }

    public Guid ShipmentId { get; set; }

    public string FromStatus { get; set; } = null!;

    public string ToStatus { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime OccurredAt { get; set; }

    public virtual Shipment? Shipment { get; set; }
}
