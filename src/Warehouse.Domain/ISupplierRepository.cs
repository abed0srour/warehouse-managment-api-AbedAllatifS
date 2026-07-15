namespace Warehouse.Domain;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ISupplierRepository
{
    Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<IEnumerable<Supplier>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(Supplier supplier, CancellationToken cancellationToken);
    Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken);
}