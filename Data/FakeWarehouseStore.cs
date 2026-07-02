using System;
using System.Collections.Generic;
using WarehouseManagement.Api.Models;

namespace WarehouseManagement.Api.Data
{
    public class FakeWarehouseStore
    {
        public static List<Product> Products { get; set; } = new List<Product>(){
            new Product
            {
                Id = 1,
                Name = "laptop",
                SKU = "SKU001",
                Description = "Description for Product 1",
                Price = 10.99m,
                QuantityInStock = 100,
                SupplierName = "Supplier A",
                ExpiryDate = DateTime.UtcNow.AddMonths(6),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = 2,
                Name = "Ipdad",
                SKU = "SKU002",
                Description = "Description for Product 2",
                Price = 15.49m,
                QuantityInStock = 50,
                SupplierName = "Supplier B",
                ExpiryDate = DateTime.UtcNow.AddMonths(12),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            }
            new Product
            {
                Id = 3,
                Name = "Iphone",
                SKU = "SKU003",
                Description = "Description for Product 3",
                Price = 7.99m,
                QuantityInStock = 200,
                SupplierName = "Supplier C",
                ExpiryDate = DateTime.UtcNow.AddMonths(3),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            }
            new Product
            {
                Id = 4,
                Name = "Apple Watch",
                SKU = "SKU004",
                Description = "Description for Product 4",
                Price = 12.75m,
                QuantityInStock = 75,
                SupplierName = "Supplier D",
                ExpiryDate = DateTime.UtcNow.AddMonths(9),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            } 
            new Product
            {
                Id = 5,
                Name = "AirPods",
                SKU = "SKU005",
                Description = "Description for Product 5",
                Price = 9.99m,
                QuantityInStock = 150,
                SupplierName = "Supplier E",
                ExpiryDate = DateTime.UtcNow.AddMonths(18),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            }
            new Product 
            {
                Id = 6,
                Name = "scanner",
                SKU = "SKU006",
                Description = "Description for Product 6",
                Price = 20.00m,
                QuantityInStock = 30,
                SupplierName = "Supplier F",
                ExpiryDate = DateTime.UtcNow.AddMonths(24),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = 7,
                Name = "Product 7",
                SKU = "SKU007",
                Description = "Description for Product 7",
                Price = 5.50m,
                QuantityInStock = 300,
                SupplierName = "Supplier G",
                ExpiryDate = DateTime.UtcNow.AddMonths(2),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = 8,
                Name = "printer",
                SKU = "SKU008",
                Description = "Description for Product 8",
                Price = 18.25m,
                QuantityInStock = 80,
                SupplierName = "Supplier H",
                ExpiryDate = DateTime.UtcNow.AddMonths(15),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = 9,
                Name = "monitor",
                SKU = "SKU009",
                Description = "Description for Product 9",
                Price = 14.99m,
                QuantityInStock = 120,
                SupplierName = "Supplier I",
                ExpiryDate = DateTime.UtcNow.AddMonths(8),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            },
            new Product
            {
                Id = 10,
                Name = "laptop",
                SKU = "SKU010",
                Description = "Description for Product 10",
                Price = 11.49m,
                QuantityInStock = 60,
                SupplierName = "Supplier J",
                ExpiryDate = DateTime.UtcNow.AddMonths(10),
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            }

        }

    }
}