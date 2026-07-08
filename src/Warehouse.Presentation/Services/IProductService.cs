using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Warehouse.Domain;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllAsync(bool onlyAvailable = false);
        Task<Product?> GetByIdAsync(Guid id);
        Task<IEnumerable<Product>> SearchAsync(string? name, string? supplier);
        Task<bool> SkuExistsAsync(string sku);
        Task<Product> CreateAsync(CreateProductRequest request);
        Task<(bool Success, Product? Product)> UpdateQuantityAsync(Guid id, int quantity);
        Task<(bool Success, decimal OldPrice, Product? Product)> UpdatePriceAsync(Guid id, decimal price);
        Task<bool> SoftDeleteAsync(Guid id);
        Task<(bool Success, Product? Product)> AssignSupplierAsync(Guid productId, Supplier supplier);
    }
}
