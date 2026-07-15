using System;
using System.Collections.Generic;

namespace Warehouse.Infrastructure.Data.EfModels;

public partial class Productimage
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string FileName { get; set; } = null!;

    public string FilePath { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
