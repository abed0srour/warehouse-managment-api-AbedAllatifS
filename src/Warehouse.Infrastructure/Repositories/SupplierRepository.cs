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
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Supplier?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.Suppliers
            .AsNoTracking()
            .Include(s => s.Products)
            .FirstOrDefaultAsync(s => s.SupplierId == id, cancellationToken);

        return entity == null ? null : ToDomain(entity);
    }

    public async Task<IEnumerable<Supplier>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.Suppliers.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    public async Task AddAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        await _context.Suppliers.AddAsync(ToEntity(supplier), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Supplier supplier, CancellationToken cancellationToken = default)
    {
        _context.Suppliers.Update(ToEntity(supplier));
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
