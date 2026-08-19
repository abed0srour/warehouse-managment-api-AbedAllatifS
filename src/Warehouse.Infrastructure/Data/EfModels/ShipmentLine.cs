using System;

namespace Warehouse.Infrastructure.Data.EfModels;

public partial class ShipmentLine
{
    public Guid Id { get; set; }

    public Guid ShipmentId { get; set; }

    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = null!;

    public string Sku { get; set; } = null!;

    public int Quantity { get; set; }

    public virtual Shipment? Shipment { get; set; }

    public virtual Product? Product { get; set; }
}
