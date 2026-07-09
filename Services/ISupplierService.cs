using System;
using System.Collections.Generic;
using WarehouseManagement.Api.Models;
using WarehouseManagement.Api.Contracts;

namespace WarehouseManagement.Api.Services
{
    public interface ISupplierService
    {
        IEnumerable<Supplier> GetAll();
        Supplier? GetById(Guid id);
        Supplier Create(CreateSupplierRequest request);
        bool Deactivate(Guid id);
    }
}