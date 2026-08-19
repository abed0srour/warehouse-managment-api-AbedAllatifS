namespace Warehouse.Infrastructure.Repositories;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        _context = context;
    }

    public Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<Shipment?> GetByReferenceNumberAsync(
        string referenceNumber,
        CancellationToken cancellationToken = default) => throw new NotImplementedException();

    public Task<IEnumerable<Shipment>> GetAllAsync(CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task AddAsync(Shipment shipment, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task UpdateAsync(Shipment shipment, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    private static void SyncLines(EfShipment entity, Shipment shipment) => throw new NotImplementedException();

    private static void SyncStatusHistory(EfShipment entity, Shipment shipment) => throw new NotImplementedException();

    private static Shipment ToDomain(EfShipment entity) => throw new NotImplementedException();

    private static ShipmentLine ToDomain(EfShipmentLine entity) => throw new NotImplementedException();

    private static ShipmentStatusChange ToDomain(EfShipmentStatusChange entity) => throw new NotImplementedException();

    private static EfShipment ToEntity(Shipment shipment) => throw new NotImplementedException();

    private static EfShipmentLine ToEntity(ShipmentLine line) => throw new NotImplementedException();

    private static EfShipmentStatusChange ToEntity(ShipmentStatusChange change) => throw new NotImplementedException();
}
