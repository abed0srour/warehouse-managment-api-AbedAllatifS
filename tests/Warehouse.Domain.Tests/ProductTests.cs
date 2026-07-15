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
}
