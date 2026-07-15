namespace Warehouse.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Warehouse.Domain;
using EfProduct = Warehouse.Infrastructure.Data.EfModels.Product;
using WarehouseDbContext = Warehouse.Infrastructure.Data.EfModels.WarehouseDbContext;

public class ProductRepository : IProductRepository
{
    private readonly WarehouseDbContext _context;

    public ProductRepository(WarehouseDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IEnumerable<Product>> GetAllAsync(CancellationToken cancellationToken)
    {
        var entities = await _context.Products.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken)
    {
        _context.Products.Add(ToEntity(product));
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken)
    {
        var entity = await _context.Products.FirstOrDefaultAsync(p => p.Id == product.Id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.Name = product.Name;
        entity.Sku = product.Sku;
        entity.Description = product.Description;
        entity.Price = product.Price;
        entity.QuantityInStock = product.QuantityInStock;
        entity.SupplierName = product.SupplierName;
        entity.SupplierId = product.SupplierId;
        entity.ExpiryDate = product.ExpiryDate;
        entity.IsArchived = product.IsArchived;
        entity.LastUpdatedAt = product.LastUpdatedAt ?? DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static Product ToDomain(EfProduct entity) => Product.Reconstruct(
        entity.Id,
        entity.Name,
        entity.Sku,
        entity.Description ?? string.Empty,
        entity.Price,
        entity.QuantityInStock,
        entity.SupplierName,
        entity.SupplierId,
        entity.ExpiryDate,
        entity.IsArchived,
        entity.CreatedAt,
        entity.LastUpdatedAt);

    private static EfProduct ToEntity(Product product) => new()
    {
        Id = product.Id,
        Name = product.Name,
        Sku = product.Sku,
        Description = product.Description,
        Price = product.Price,
        QuantityInStock = product.QuantityInStock,
        SupplierName = product.SupplierName,
        SupplierId = product.SupplierId,
        ExpiryDate = product.ExpiryDate,
        IsArchived = product.IsArchived,
        CreatedAt = product.CreatedAt,
        LastUpdatedAt = product.LastUpdatedAt ?? product.CreatedAt
    };
}
