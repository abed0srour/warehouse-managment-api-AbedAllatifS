namespace Warehouse.Domain;

using System;

public class WarehouseItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string LocationCode { get; set; } = string.Empty; // e.g., "Aisle-12-Shelf-C"
    public int TotalQuantity { get; set; }
}