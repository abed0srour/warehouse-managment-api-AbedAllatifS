using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Warehouse.Domain;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Services
{
    public interface ISupplierService
    {
        Task<IEnumerable<Supplier>> GetAllAsync();
        Task<Supplier?> GetByIdAsync(Guid id);
        Task<Supplier> CreateAsync(CreateSupplierRequest request);
        Task<bool> DeactivateAsync(Guid id);
    }
}