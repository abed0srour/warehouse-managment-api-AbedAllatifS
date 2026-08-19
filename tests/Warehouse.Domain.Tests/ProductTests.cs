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

    [Fact]
    public void AdjustStock_WithPositiveChange_ShouldIncreaseTheLevel()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        product.AdjustStock(5);

        Assert.Equal(15, product.QuantityInStock);
        Assert.NotNull(product.LastUpdatedAt);
    }

    [Fact]
    public void AdjustStock_WithNegativeChange_ShouldDecreaseTheLevel()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        product.AdjustStock(-4);

        Assert.Equal(6, product.QuantityInStock);
    }

    [Fact]
    public void AdjustStock_IsRelative_SoRepeatedCallsAccumulate()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        product.AdjustStock(5);
        product.AdjustStock(5);

        Assert.Equal(20, product.QuantityInStock);
    }

    [Fact]
    public void AdjustStock_DownToExactlyZero_ShouldBeAllowed()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        product.AdjustStock(-10);

        Assert.Equal(0, product.QuantityInStock);
    }

    [Fact]
    public void AdjustStock_BelowZero_ShouldThrowInvalidOperationException()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        var ex = Assert.Throws<InvalidOperationException>(() => product.AdjustStock(-11));

        Assert.Equal("Cannot decrease stock by 11: only 10 in stock.", ex.Message);
        Assert.Equal(10, product.QuantityInStock);
    }

    [Fact]
    public void AdjustStock_WithZero_ShouldThrowArgumentException()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        var ex = Assert.Throws<ArgumentException>(() => product.AdjustStock(0));

        Assert.Equal("Stock adjustment cannot be zero.", ex.Message);
    }

    [Fact]
    public void AdjustStock_WhenProductIsArchived_ShouldThrowInvalidOperationException()
    {
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);
        product.Archive();

        var ex = Assert.Throws<InvalidOperationException>(() => product.AdjustStock(1));

        Assert.Equal("Archived products cannot be updated.", ex.Message);
        Assert.Equal(10, product.QuantityInStock);
    }

    [Fact]
    public void AdjustStock_ThatWouldOverflowInt_ShouldThrowRatherThanWrapNegative()
    {
        // The addition is widened to long first: an unwidened QuantityInStock + int.MaxValue
        // wraps to a negative number, which would slip past the below-zero guard and leave
        // the product holding negative stock.
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        var ex = Assert.Throws<InvalidOperationException>(() => product.AdjustStock(int.MaxValue));

        Assert.Equal("Resulting stock quantity is too large.", ex.Message);
        Assert.Equal(10, product.QuantityInStock);
    }

    [Fact]
    public void AdjustStock_WithIntMinValue_ShouldReportTheMagnitudeWithoutOverflowing()
    {
        // Math.Abs(int.MinValue) throws; the message builder widens to long to avoid it.
        var product = Product.Create("Laptop", "SKU-001", 999.99m, 10);

        var ex = Assert.Throws<InvalidOperationException>(() => product.AdjustStock(int.MinValue));

        Assert.Equal("Cannot decrease stock by 2147483648: only 10 in stock.", ex.Message);
    }
}
