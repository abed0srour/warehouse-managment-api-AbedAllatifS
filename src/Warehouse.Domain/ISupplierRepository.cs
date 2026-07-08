namespace Warehouse.Domain;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id);
    Task<IEnumerable<Supplier>> GetAllAsync();
    Task AddAsync(Supplier supplier);
    Task UpdateAsync(Supplier supplier);
}