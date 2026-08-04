using Warehouse.Domain;

namespace Warehouse.Domain.Tests;

public class ProductTests
{
    [Fact]
    public void Create_ShouldInitializeProductWithProvidedValues()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        Assert.Equal("Laptop", product.Name);
        Assert.Equal("SKU-001", product.Sku);
        Assert.Equal(999.99m, product.Price);
        Assert.Equal(10, product.QuantityInStock);
        Assert.False(product.IsArchived);
    }

    [Fact]
    public void AssignSupplier_WhenProductIsArchived_ShouldThrowInvalidOperationException()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);
        product.Archive();
        var supplier = new Supplier { Name = "Acme Supplies", IsActive = true };

        var ex = Assert.Throws<InvalidOperationException>(() => product.AssignSupplier(supplier));

        Assert.Equal("Archived products cannot be updated.", ex.Message);
        Assert.Null(product.SupplierId);
        Assert.Null(product.SupplierName);
        Assert.Null(product.Supplier);
    }

    [Fact]
    public void AssignSupplier_WhenProductIsArchivedAndSupplierIsInactive_ShouldThrowForArchivedFirst()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);
        product.Archive();
        var supplier = new Supplier { Name = "Acme Supplies", IsActive = false };

        var ex = Assert.Throws<InvalidOperationException>(() => product.AssignSupplier(supplier));

        Assert.Equal("Archived products cannot be updated.", ex.Message);
    }

    [Fact]
    public void AssignSupplier_WhenSupplierIsNull_ShouldThrowArgumentNullException()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        Assert.Throws<ArgumentNullException>(() => product.AssignSupplier(null!));
    }

    [Fact]
    public void AssignSupplier_WhenProductIsActiveAndSupplierIsActive_ShouldAssign()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);
        var supplier = new Supplier { Name = "Acme Supplies", IsActive = true };

        product.AssignSupplier(supplier);

        Assert.Equal(supplier.Id, product.SupplierId);
        Assert.Equal("Acme Supplies", product.SupplierName);
        Assert.Same(supplier, product.Supplier);
        Assert.NotNull(product.LastUpdatedAt);
    }
}
