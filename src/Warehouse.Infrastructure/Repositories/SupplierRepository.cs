namespace Warehouse.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Warehouse.Domain;
using Warehouse.Infrastructure.Data;

public class SupplierRepository : ISupplierRepository
{
    public Task<Supplier?> GetByIdAsync(Guid id)
    {
        var supplier = FakeWarehouseStore.Suppliers.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(supplier);
    }

    public Task<IEnumerable<Supplier>> GetAllAsync()
    {
        return Task.FromResult<IEnumerable<Supplier>>(FakeWarehouseStore.Suppliers);
    }

    public Task AddAsync(Supplier supplier)
    {
        FakeWarehouseStore.Suppliers.Add(supplier);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Supplier supplier)
    {
        var index = FakeWarehouseStore.Suppliers.FindIndex(s => s.Id == supplier.Id);
        if (index != -1)
        {
            FakeWarehouseStore.Suppliers[index] = supplier;
        }
        return Task.CompletedTask;
    }
}