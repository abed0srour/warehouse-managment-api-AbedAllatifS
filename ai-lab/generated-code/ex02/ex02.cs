using System;
using Xunit;

namespace Warehouse.Domain
{
    public partial class Product
    {
        public void AssignSupplier(Supplier supplier)
        {
            ArgumentNullException.ThrowIfNull(supplier);

            if (IsArchived)
                throw new InvalidOperationException("Archived products cannot be updated.");

            if (!supplier.IsActive)
                throw new InvalidOperationException("Inactive suppliers cannot be assigned to products.");

            SupplierId = supplier.Id;
            SupplierName = supplier.Name;
            Supplier = supplier;
            LastUpdatedAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        }
    }
}

namespace Warehouse.Domain.Tests
{
    public partial class ProductTests
    {
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
}
