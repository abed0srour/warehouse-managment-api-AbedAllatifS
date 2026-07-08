namespace Warehouse.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface IProductRepository
{
    Task<Product?> GetByIdAsync(Guid id);
    Task<IEnumerable<Product>> GetAllAsync();
    Task AddAsync(Product product);
    Task UpdateAsync(Product product);
}