namespace Warehouse.Domain;

public interface IShipmentRepository : IRepository<Shipment>
{
    /// <summary>
    /// Reference-number lookup used to enforce uniqueness on create. Deliberately a targeted
    /// query rather than a GetAllAsync scan, so adding shipments does not get slower over time.
    /// </summary>
    Task<Shipment?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken cancellationToken = default);
}
