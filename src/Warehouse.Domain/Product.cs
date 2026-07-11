namespace Warehouse.Domain;

using System;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; private set; }
    public int QuantityInStock { get; private set; }
    public string? SupplierName { get; set; }
    public Guid? SupplierId { get; private set; } 
    public DateTime? ExpiryDate { get; set; }
    public bool IsArchived { get; private set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAt { get; set; }

    public virtual Supplier? Supplier { get; set; }

    public static Product Create(string name, string sku, decimal price, int quantity)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required."); 

        if (string.IsNullOrWhiteSpace(sku))
            throw new ArgumentException("SKU is required."); 

        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero."); 

        if (quantity < 0)
            throw new ArgumentException("Quantity cannot be negative."); 

        return new Product
        {
            Name = name,
            Sku = sku,
            Price = price,
            QuantityInStock = quantity
        };
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (IsArchived)
            throw new InvalidOperationException("Archived products cannot be updated.");

        if (newPrice <= 0)
            throw new ArgumentException("Price must be greater than zero.");

        Price = newPrice;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void UpdateQuantity(int newQuantity)
    {
        if (IsArchived)
            throw new InvalidOperationException("Archived products cannot be updated.");

        if (newQuantity < 0)
            throw new ArgumentException("Quantity cannot be negative.");

        QuantityInStock = newQuantity;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        IsArchived = true;
        LastUpdatedAt = DateTime.UtcNow;
    }

    public void AssignSupplier(Supplier supplier)
    {
        if (IsArchived)
            throw new InvalidOperationException("Archived products cannot be updated.");

        if (!supplier.IsActive)
            throw new InvalidOperationException("Inactive suppliers cannot be assigned to products.");

        SupplierId = supplier.Id;
        SupplierName = supplier.Name;
        Supplier = supplier; 
        LastUpdatedAt = DateTime.UtcNow;
    }
}