namespace Warehouse.Domain;

using System;

public class ProductImage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
}