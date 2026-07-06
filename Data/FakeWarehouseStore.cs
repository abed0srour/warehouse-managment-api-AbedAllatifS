using System;
using System.Collections.Generic;
using WarehouseManagement.Api.Models;

namespace WarehouseManagement.Api.Data
{
    public class FakeWarehouseStore
    {
        public static List<Product> Products { get; set; } = new List<Product>
        {
            new Product
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Name = "laptop",
                SKU = "SKU001",
                Description = "High-performance mobile workstation",
                Price = 1099.99m,
                QuantityInStock = 100,
                SupplierName = "Supplier A",
                ExpiryDate = DateTime.UtcNow.AddMonths(12),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                Name = "mouse",
                SKU = "SKU002",
                Description = "Wireless optical mouse",
                Price = 29.99m,
                QuantityInStock = 150,
                SupplierName = "Supplier B",
                ExpiryDate = DateTime.UtcNow.AddMonths(24),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Name = "keyboard",
                SKU = "SKU003",
                Description = "Mechanical keyboard with RGB lighting",
                Price = 79.99m,
                QuantityInStock = 85,
                SupplierName = "Supplier C",
                ExpiryDate = DateTime.UtcNow.AddMonths(18),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "scanner",
                SKU = "SKU004",
                Description = "Compact document scanner",
                Price = 129.50m,
                QuantityInStock = 40,
                SupplierName = "Supplier D",
                ExpiryDate = DateTime.UtcNow.AddMonths(36),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = Guid.Parse("55555555-5555-5555-5555-555555555555"),
                Name = "printer",
                SKU = "SKU005",
                Description = "Color laser printer",
                Price = 249.99m,
                QuantityInStock = 55,
                SupplierName = "Supplier E",
                ExpiryDate = DateTime.UtcNow.AddMonths(48),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = Guid.Parse("66666666-6666-6666-6666-666666666666"),
                Name = "monitor",
                SKU = "SKU006",
                Description = "27-inch full HD monitor",
                Price = 189.99m,
                QuantityInStock = 95,
                SupplierName = "Supplier F",
                ExpiryDate = DateTime.UtcNow.AddMonths(30),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
                Name = "webcam",
                SKU = "SKU007",
                Description = "HD webcam with built-in microphone",
                Price = 59.99m,
                QuantityInStock = 120,
                SupplierName = "Supplier G",
                ExpiryDate = DateTime.UtcNow.AddMonths(20),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = Guid.Parse("88888888-8888-8888-8888-888888888888"),
                Name = "headset",
                SKU = "SKU008",
                Description = "Noise-cancelling over-ear headset",
                Price = 89.99m,
                QuantityInStock = 70,
                SupplierName = "Supplier H",
                ExpiryDate = DateTime.UtcNow.AddMonths(22),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = Guid.Parse("99999999-9999-9999-9999-999999999999"),
                Name = "tablet",
                SKU = "SKU009",
                Description = "10-inch Android tablet",
                Price = 299.99m,
                QuantityInStock = 45,
                SupplierName = "Supplier I",
                ExpiryDate = DateTime.UtcNow.AddMonths(30),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                Name = "router",
                SKU = "SKU010",
                Description = "Dual-band Wi-Fi router",
                Price = 79.99m,
                QuantityInStock = 130,
                SupplierName = "Supplier J",
                ExpiryDate = DateTime.UtcNow.AddMonths(60),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            }
        };

        public static List<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

        public static List<Supplier> Suppliers { get; set; } = new List<Supplier>
        {
            new Supplier
            {
                Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                Name = "Supplier A",
                Country = "Lebanon",
                ContactEmail = "info@supplierA.com",
                PhoneNumber = "+9610123456",
                IsActive = true
            }
        };
    }
}
