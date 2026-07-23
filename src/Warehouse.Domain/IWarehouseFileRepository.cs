namespace Warehouse.Domain;

public interface IWarehouseFileRepository : IRepository<WarehouseFile>
{
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
