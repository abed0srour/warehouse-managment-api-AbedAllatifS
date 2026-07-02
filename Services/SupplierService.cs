using System;
using System.Collections.Generic;
using System.Linq;
using WarehouseManagement.Api.Data;
using WarehouseManagement.Api.Models;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Services
{
    public class SupplierService : ISupplierService
    {
        // GET all suppliers
        public IEnumerable<Supplier> GetAll()
        {
            return FakeWarehouseStore.Suppliers;
        }

        // GET supplier by id
        public Supplier? GetById(Guid id)
        {
            return FakeWarehouseStore.Suppliers.FirstOrDefault(s => s.Id == id);
        }

        // POST create supplier
        public Supplier Create(CreateSupplierRequest request)
        {
            var supplier = new Supplier
            {
                Id = Guid.NewGuid(), // Generate unique ID
                Name = request.Name,
                Country = request.Country,
                ContactEmail = request.ContactEmail,
                PhoneNumber = request.PhoneNumber,
                IsActive = true // Active by default
            };

            FakeWarehouseStore.Suppliers.Add(supplier);
            return supplier;
        }

        // DELETE deactivate supplier (Soft delete rule)
        public bool Deactivate(Guid id)
        {
            var supplier = GetById(id);
            if (supplier == null) 
            {
                return false;
            }

            supplier.IsActive = false; // Turn active flag to false
            return true;
        }
    }
}