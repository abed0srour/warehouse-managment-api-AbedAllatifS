using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Warehouse.Domain;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Services
{
    public class SupplierService : ISupplierService
    {
        private readonly ISupplierRepository _supplierRepository;

        public SupplierService(ISupplierRepository supplierRepository)
        {
            _supplierRepository = supplierRepository;
        }

        public Task<IEnumerable<Supplier>> GetAllAsync() => _supplierRepository.GetAllAsync();

        public Task<Supplier?> GetByIdAsync(Guid id) => _supplierRepository.GetByIdAsync(id);

        public async Task<Supplier> CreateAsync(CreateSupplierRequest request)
        {
            var supplier = new Supplier
            {
                Name = request.Name,
                IsActive = true
            };

            await _supplierRepository.AddAsync(supplier);
            return supplier;
        }

        public async Task<bool> DeactivateAsync(Guid id)
        {
            var supplier = await _supplierRepository.GetByIdAsync(id);
            if (supplier == null)
            {
                return false;
            }

            supplier.IsActive = false;
            await _supplierRepository.UpdateAsync(supplier);
            return true;
        }
    }
}