using System;
using System.Collections.Generic;
using WarehouseManagement.Api.Contracts;
using WarehouseManagement.Api.Models;

namespace WarehouseManagement.Api.Services
{
    public interface IProductService
    {
        IEnumerable<Product> GetAll(bool onlyAvailable = false);
        Product? GetById(Guid id);
        IEnumerable<Product> Search(string? name, string? supplier);
        bool SkuExists(string sku);
        Product Create(CreateProductRequest request);
        bool UpdateQuantity(Guid id, int quantity, out Product? product);
        bool UpdatePrice(Guid id, decimal price, out decimal oldPrice, out Product? product);
        bool SoftDelete(Guid id);
        bool AssignSupplier(Guid productId, Supplier supplier, out Product? product);
    }
}
