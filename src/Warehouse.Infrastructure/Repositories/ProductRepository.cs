namespace Warehouse.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Warehouse.Domain;
using Warehouse.Infrastructure.Data;

public class ProductRepository : IProductRepository
{
    public Task<Product?> GetByIdAsync(Guid id)
    {
        var product = FakeWarehouseStore.Products.FirstOrDefault(p => p.Id == id);
        return Task.FromResult(product);
    }

    public Task<IEnumerable<Product>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<Product>>(FakeWarehouseStore.Products);
    }

    public Task AddAsync(Product product)
    {
        FakeWarehouseStore.Products.Add(product);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Product product)
    {
        var index = FakeWarehouseStore.Products.FindIndex(p => p.Id == product.Id);
        if (index != -1)
        {
            FakeWarehouseStore.Products[index] = product;
        }
        return Task.CompletedTask;
    }
}