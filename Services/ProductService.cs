using System;
using System.Collections.Generic;
using System.Linq;
using WarehouseManagement.Api.Contracts;
using WarehouseManagement.Api.Data;
using WarehouseManagement.Api.Models;

namespace WarehouseManagement.Api.Services
{
    public class ProductService : IProductService
    {
        public IEnumerable<Product> GetAll(bool onlyAvailable = false)
        {
            var query = FakeWarehouseStore.Products.AsEnumerable();
            if (onlyAvailable)
            {
                query = query.Where(p => p.QuantityInStock > 0);
            }
            return query.OrderByDescending(p => p.CreatedAt);
        }

        public Product? GetById(Guid id)
        {
            return FakeWarehouseStore.Products.FirstOrDefault(p => p.Id == id);
        }

        public IEnumerable<Product> Search(string? name, string? supplier)
        {
            var query = FakeWarehouseStore.Products.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(supplier))
            {
                query = query.Where(p => p.SupplierName.Contains(supplier, StringComparison.OrdinalIgnoreCase));
            }

            return query;
        }

        public bool SkuExists(string sku)
        {
            return FakeWarehouseStore.Products.Any(p => p.SKU.Equals(sku, StringComparison.OrdinalIgnoreCase));
        }

        public Product Create(CreateProductRequest request)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(),
                Name = request.Name,
                SKU = request.SKU,
                Description = request.Description,
                Price = request.Price,
                QuantityInStock = request.QuantityInStock,
                SupplierName = request.SupplierName,
                ExpiryDate = request.ExpiryDate,
                IsArchived = false,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = null
            };

            FakeWarehouseStore.Products.Add(product);
            return product;
        }

        public bool UpdateQuantity(Guid id, int quantity, out Product? product)
        {
            product = GetById(id);
            if (product == null)
            {
                return false;
            }

            product.QuantityInStock = quantity;
            product.LastUpdatedAt = DateTime.UtcNow;
            return true;
        }

        public bool UpdatePrice(Guid id, decimal price, out decimal oldPrice, out Product? product)
        {
            product = GetById(id);
            if (product == null)
            {
                oldPrice = 0;
                return false;
            }

            oldPrice = product.Price;
            product.Price = price;
            product.LastUpdatedAt = DateTime.UtcNow;
            return true;
        }

        public bool SoftDelete(Guid id)
        {
            var product = GetById(id);
            if (product == null)
            {
                return false;
            }

            product.IsArchived = true;
            product.LastUpdatedAt = DateTime.UtcNow;
            return true;
        }

        public bool AssignSupplier(Guid productId, Supplier supplier, out Product? product)
        {
            product = GetById(productId);
            if (product == null)
            {
                return false;
            }

            if (product.IsArchived)
            {
                return false;
            }

            product.SupplierName = supplier.Name;
            product.LastUpdatedAt = DateTime.UtcNow;
            return true;
        }
    }
}
