using System;
using System.Collections.Generic;

namespace Warehouse.Infrastructure.Data.EfModels;

public partial class Product
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Sku { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int QuantityInStock { get; set; }

    public string? SupplierName { get; set; }

    public Guid? SupplierId { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime LastUpdatedAt { get; set; }

    public virtual ICollection<Productimage> Productimages { get; set; } = new List<Productimage>();

    public virtual Supplier? Supplier { get; set; }
}
