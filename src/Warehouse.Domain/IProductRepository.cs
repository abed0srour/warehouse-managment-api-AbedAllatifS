namespace Warehouse.Domain;

public interface IProductRepository : IRepository<Product>
{
    /// <summary>
    /// Fetches several products in one round trip. Callers that need a set of products
    /// (assigning a batch to a shipment, for instance) use this instead of looping over
    /// <see cref="IRepository{T}.GetByIdAsync"/>, which would issue one query per id.
    /// </summary>
    Task<IReadOnlyList<Product>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
}
