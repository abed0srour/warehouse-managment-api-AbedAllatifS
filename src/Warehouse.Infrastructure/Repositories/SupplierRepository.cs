namespace Warehouse.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Warehouse.Domain;
using EfSupplier = Warehouse.Infrastructure.Data.EfModels.Supplier;
using WarehouseDbContext = Warehouse.Infrastructure.Data.EfModels.WarehouseDbContext;

public class SupplierRepository : ISupplierRepository
{
    private readonly WarehouseDbContext _context;

    public SupplierRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == id, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IEnumerable<Supplier>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await _context.Suppliers.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        _context.Suppliers.Add(ToEntity(supplier));
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken)
    {
        var entity = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == supplier.Id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Name = supplier.Name;
        entity.Country = supplier.Country;
        entity.ContactEmail = supplier.ContactEmail;
        entity.PhoneNumber = supplier.PhoneNumber;
        entity.IsActive = supplier.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static Supplier ToDomain(EfSupplier entity) => new()
    {
        Id = entity.SupplierId,
        Name = entity.Name,
        Country = entity.Country,
        ContactEmail = entity.ContactEmail,
        PhoneNumber = entity.PhoneNumber,
        IsActive = entity.IsActive
    };

    private static EfSupplier ToEntity(Supplier supplier) => new()
    {
        SupplierId = supplier.Id,
        Name = supplier.Name,
        Country = supplier.Country,
        ContactEmail = supplier.ContactEmail,
        PhoneNumber = supplier.PhoneNumber,
        IsActive = supplier.IsActive
    };
}
