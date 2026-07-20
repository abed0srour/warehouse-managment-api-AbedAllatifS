using System;
using System.Collections.Generic;

namespace Warehouse.Infrastructure.Data.EfModels;

public partial class Supplier
{
    public Guid SupplierId { get; set; }

    public string Name { get; set; } = null!;

    public string Country { get; set; } = null!;

    public string ContactEmail { get; set; } = null!;

    public string PhoneNumber { get; set; } = null!;

    public bool IsActive { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
