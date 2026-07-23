namespace Warehouse.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Warehouse.Domain;
using EfWarehouseFile = Warehouse.Infrastructure.Data.EfModels.WarehouseFile;
using WarehouseDbContext = Warehouse.Infrastructure.Data.EfModels.WarehouseDbContext;

public class WarehouseFileRepository : IWarehouseFileRepository
{
    private readonly WarehouseDbContext _context;

    public WarehouseFileRepository(WarehouseDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<WarehouseFile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.WarehouseFiles
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

        return entity == null ? null : ToDomain(entity);
    }

    public async Task<IEnumerable<WarehouseFile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _context.WarehouseFiles.AsNoTracking().ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    public async Task AddAsync(WarehouseFile file, CancellationToken cancellationToken = default)
    {
        await _context.WarehouseFiles.AddAsync(ToEntity(file), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(WarehouseFile file, CancellationToken cancellationToken = default)
    {
        _context.WarehouseFiles.Update(ToEntity(file));
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.WarehouseFiles.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (entity == null)
        {
            return;
        }

        _context.WarehouseFiles.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static WarehouseFile ToDomain(EfWarehouseFile entity) => new()
    {
        Id = entity.Id,
        ObjectKey = entity.ObjectKey,
        FileName = entity.FileName,
        ContentType = entity.ContentType,
        SizeBytes = entity.SizeBytes,
        UploadedByUid = entity.UploadedByUid,
        RelatedEntityType = entity.RelatedEntityType,
        RelatedEntityId = entity.RelatedEntityId,
        UploadedAtUtc = entity.UploadedAtUtc
    };

    private static EfWarehouseFile ToEntity(WarehouseFile file) => new()
    {
        Id = file.Id,
        ObjectKey = file.ObjectKey,
        FileName = file.FileName,
        ContentType = file.ContentType,
        SizeBytes = file.SizeBytes,
        UploadedByUid = file.UploadedByUid,
        RelatedEntityType = file.RelatedEntityType,
        RelatedEntityId = file.RelatedEntityId,
        UploadedAtUtc = DateTime.SpecifyKind(file.UploadedAtUtc, DateTimeKind.Utc)
    };
}
