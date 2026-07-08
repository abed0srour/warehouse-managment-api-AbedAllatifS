using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Warehouse.Domain;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Services
{
    public class ProductService : IProductService
    {
        private readonly IProductRepository _productRepository;

        public ProductService(IProductRepository productRepository)
        {
            _productRepository = productRepository;
        }

        public async Task<IEnumerable<Product>> GetAllAsync(bool onlyAvailable = false)
        {
            var products = await _productRepository.GetAllAsync();
            return onlyAvailable ? products.Where(p => p.QuantityInStock > 0).OrderByDescending(p => p.CreatedAt) : products.OrderByDescending(p => p.CreatedAt);
        }

        public Task<Product?> GetByIdAsync(Guid id) => _productRepository.GetByIdAsync(id);

        public async Task<IEnumerable<Product>> SearchAsync(string? name, string? supplier)
        {
            var products = await _productRepository.GetAllAsync();
            var query = products.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(name))
            {
                query = query.Where(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(supplier))
            {
                query = query.Where(p => (p.SupplierName ?? string.Empty).Contains(supplier, StringComparison.OrdinalIgnoreCase));
            }

            return query.ToList();
        }

        public async Task<bool> SkuExistsAsync(string sku)
        {
            var products = await _productRepository.GetAllAsync();
            return products.Any(p => p.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<Product> CreateAsync(CreateProductRequest request)
        {
            var product = Product.Create(request.Name, request.SKU, request.Price, request.QuantityInStock);
            product.Description = request.Description;
            product.ExpiryDate = request.ExpiryDate;
            await _productRepository.AddAsync(product);
            return product;
        }

        public async Task<(bool Success, Product? Product)> UpdateQuantityAsync(Guid id, int quantity)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return (false, null);
            }

            product.UpdateQuantity(quantity);
            await _productRepository.UpdateAsync(product);
            return (true, product);
        }

        public async Task<(bool Success, decimal OldPrice, Product? Product)> UpdatePriceAsync(Guid id, decimal price)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return (false, 0, null);
            }

            var oldPrice = product.Price;
            product.UpdatePrice(price);
            await _productRepository.UpdateAsync(product);
            return (true, oldPrice, product);
        }

        public async Task<bool> SoftDeleteAsync(Guid id)
        {
            var product = await _productRepository.GetByIdAsync(id);
            if (product == null)
            {
                return false;
            }

            product.Archive();
            await _productRepository.UpdateAsync(product);
            return true;
        }

        public async Task<(bool Success, Product? Product)> AssignSupplierAsync(Guid productId, Supplier supplier)
        {
            var product = await _productRepository.GetByIdAsync(productId);
            if (product == null)
            {
                return (false, null);
            }

            product.AssignSupplier(supplier);
            await _productRepository.UpdateAsync(product);
            return (true, product);
        }
    }
}
