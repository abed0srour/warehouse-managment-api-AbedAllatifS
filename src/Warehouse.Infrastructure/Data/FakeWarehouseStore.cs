namespace Warehouse.Infrastructure.Data;

using System.Collections.Generic;
using Warehouse.Domain;

public static class FakeWarehouseStore
{
    public static List<Product> Products { get; set; } = new();
    public static List<Supplier> Suppliers { get; set; } = new();
}