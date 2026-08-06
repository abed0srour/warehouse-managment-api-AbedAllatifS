using MediatR;

namespace Warehouse.Application.Products.Queries;

/// <summary>
/// A product that has run down to zero. Narrower than <see cref="ProductViewModel"/> on
/// purpose: a restocking report needs identity and who to reorder from, not price or expiry.
/// </summary>
public record OutOfStockProductDto(
    Guid Id,
    string Name,
    string Sku,
    Guid? SupplierId,
    string? SupplierName,
    DateTime? LastUpdatedAt);

/// <summary>
/// Every unarchived product whose QuantityInStock is exactly zero. Handled in
/// Warehouse.Infrastructure so the filter runs in the database rather than over a
/// GetAllAsync result in memory.
/// </summary>
public record GetOutOfStockProductsQuery : IRequest<IEnumerable<OutOfStockProductDto>>;
