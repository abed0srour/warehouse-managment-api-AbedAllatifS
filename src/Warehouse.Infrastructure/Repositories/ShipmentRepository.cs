namespace Warehouse.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Warehouse.Domain;
using EfShipment = Warehouse.Infrastructure.Data.EfModels.Shipment;
using EfShipmentLine = Warehouse.Infrastructure.Data.EfModels.ShipmentLine;
using EfShipmentStatusChange = Warehouse.Infrastructure.Data.EfModels.ShipmentStatusChange;
using WarehouseDbContext = Warehouse.Infrastructure.Data.EfModels.WarehouseDbContext;

public class ShipmentRepository : IShipmentRepository
{
    private readonly WarehouseDbContext _context;

    public ShipmentRepository(WarehouseDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await ReadQuery().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        return entity == null ? null : ToDomain(entity);
    }

    public async Task<Shipment?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken cancellationToken = default)
    {
        var entity = await ReadQuery()
            .FirstOrDefaultAsync(s => s.ReferenceNumber == referenceNumber, cancellationToken);

        return entity == null ? null : ToDomain(entity);
    }

    public async Task<IEnumerable<Shipment>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await ReadQuery().ToListAsync(cancellationToken);
        return entities.Select(ToDomain).ToList();
    }

    public async Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        // The whole graph is new, so EF inserts the shipment with its lines and history in one go.
        await _context.Shipments.AddAsync(ToEntity(shipment), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Loads the tracked aggregate and reconciles it against the in-memory one.
    /// Deliberately not <c>DbSet.Update(detachedGraph)</c>: that marks every child as Modified,
    /// which throws on newly added lines and silently leaves removed ones behind.
    /// </summary>
    public async Task UpdateAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shipment);

        var entity = await _context.Shipments
            .Include(s => s.Lines)
            .Include(s => s.StatusHistory)
            .FirstOrDefaultAsync(s => s.Id == shipment.Id, cancellationToken);

        if (entity == null)
        {
            await AddAsync(shipment, cancellationToken);
            return;
        }

        entity.ReferenceNumber = shipment.ReferenceNumber;
        entity.SupplierId = shipment.SupplierId;
        entity.SupplierName = shipment.SupplierName;
        entity.Status = shipment.Status.ToString();
        entity.DestinationLine1 = shipment.Destination.Line1;
        entity.DestinationCity = shipment.Destination.City;
        entity.DestinationPostalCode = shipment.Destination.PostalCode;
        entity.DestinationCountry = shipment.Destination.Country;
        entity.ExpectedDeliveryDate = AsUnspecified(shipment.ExpectedDeliveryDate);
        entity.TrackingNumber = shipment.TrackingNumber;
        entity.DispatchedAt = AsUnspecified(shipment.DispatchedAt);
        entity.DeliveredAt = AsUnspecified(shipment.DeliveredAt);
        entity.SupplierNotifiedAt = AsUnspecified(shipment.SupplierNotifiedAt);
        entity.LastUpdatedAt = AsUnspecified(shipment.LastUpdatedAt);

        SyncLines(entity, shipment);
        SyncStatusHistory(entity, shipment);

        await _context.SaveChangesAsync(cancellationToken);
    }

    // Both child collections are pulled in the same round trip -- no lazy loading, no N+1.
    private IQueryable<EfShipment> ReadQuery() =>
        _context.Shipments
            .AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.StatusHistory);

    private static void SyncLines(EfShipment entity, Shipment shipment)
    {
        var desired = shipment.Lines.ToDictionary(l => l.Id);

        foreach (var orphan in entity.Lines.Where(l => !desired.ContainsKey(l.Id)).ToList())
        {
            entity.Lines.Remove(orphan);
        }

        foreach (var line in shipment.Lines)
        {
            var existing = entity.Lines.FirstOrDefault(l => l.Id == line.Id);
            if (existing == null)
            {
                entity.Lines.Add(ToEntity(line));
                continue;
            }

            existing.ProductId = line.ProductId;
            existing.ProductName = line.ProductName;
            existing.Sku = line.Sku;
            existing.Quantity = line.Quantity;
        }
    }

    // Status history is append-only, so existing rows are never touched or removed.
    private static void SyncStatusHistory(EfShipment entity, Shipment shipment)
    {
        var persisted = entity.StatusHistory.Select(h => h.Id).ToHashSet();

        foreach (var change in shipment.StatusHistory.Where(h => !persisted.Contains(h.Id)))
        {
            entity.StatusHistory.Add(ToEntity(change));
        }
    }

    private static Shipment ToDomain(EfShipment entity) => Shipment.Reconstruct(
        entity.Id,
        entity.ReferenceNumber,
        entity.SupplierId,
        entity.SupplierName,
        ParseStatus(entity.Status),
        Address.Reconstruct(
            entity.DestinationLine1,
            entity.DestinationCity,
            entity.DestinationPostalCode,
            entity.DestinationCountry),
        entity.ExpectedDeliveryDate,
        entity.TrackingNumber,
        entity.DispatchedAt,
        entity.DeliveredAt,
        entity.SupplierNotifiedAt,
        entity.CreatedAt,
        entity.LastUpdatedAt,
        entity.Lines.Select(ToDomain).ToList(),
        entity.StatusHistory.Select(ToDomain).ToList());

    private static ShipmentLine ToDomain(EfShipmentLine entity) => ShipmentLine.Reconstruct(
        entity.Id,
        entity.ShipmentId,
        entity.ProductId,
        entity.ProductName,
        entity.Sku,
        entity.Quantity);

    private static ShipmentStatusChange ToDomain(EfShipmentStatusChange entity) => ShipmentStatusChange.Reconstruct(
        entity.Id,
        entity.ShipmentId,
        ParseStatus(entity.FromStatus),
        ParseStatus(entity.ToStatus),
        entity.Note,
        entity.OccurredAt);

    private static EfShipment ToEntity(Shipment shipment) => new()
    {
        Id = shipment.Id,
        ReferenceNumber = shipment.ReferenceNumber,
        SupplierId = shipment.SupplierId,
        SupplierName = shipment.SupplierName,
        Status = shipment.Status.ToString(),
        DestinationLine1 = shipment.Destination.Line1,
        DestinationCity = shipment.Destination.City,
        DestinationPostalCode = shipment.Destination.PostalCode,
        DestinationCountry = shipment.Destination.Country,
        ExpectedDeliveryDate = AsUnspecified(shipment.ExpectedDeliveryDate),
        TrackingNumber = shipment.TrackingNumber,
        DispatchedAt = AsUnspecified(shipment.DispatchedAt),
        DeliveredAt = AsUnspecified(shipment.DeliveredAt),
        SupplierNotifiedAt = AsUnspecified(shipment.SupplierNotifiedAt),
        CreatedAt = DateTime.SpecifyKind(shipment.CreatedAt, DateTimeKind.Unspecified),
        LastUpdatedAt = AsUnspecified(shipment.LastUpdatedAt),
        Lines = shipment.Lines.Select(ToEntity).ToList(),
        StatusHistory = shipment.StatusHistory.Select(ToEntity).ToList()
    };

    private static EfShipmentLine ToEntity(ShipmentLine line) => new()
    {
        Id = line.Id,
        ShipmentId = line.ShipmentId,
        ProductId = line.ProductId,
        ProductName = line.ProductName,
        Sku = line.Sku,
        Quantity = line.Quantity
    };

    private static EfShipmentStatusChange ToEntity(ShipmentStatusChange change) => new()
    {
        Id = change.Id,
        ShipmentId = change.ShipmentId,
        FromStatus = change.FromStatus.ToString(),
        ToStatus = change.ToStatus.ToString(),
        Note = change.Note,
        OccurredAt = DateTime.SpecifyKind(change.OccurredAt, DateTimeKind.Unspecified)
    };

    private static ShipmentStatus ParseStatus(string value) =>
        Enum.TryParse<ShipmentStatus>(value, ignoreCase: true, out var parsed)
            ? parsed
            : ShipmentStatus.Draft;

    private static DateTime? AsUnspecified(DateTime? value) =>
        value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified) : null;
}
