namespace Warehouse.Domain;

public interface IShipmentRepository : IRepository<Shipment>
{
    Task<Shipment?> GetByReferenceNumberAsync(
        string referenceNumber,
        CancellationToken cancellationToken = default);
}
