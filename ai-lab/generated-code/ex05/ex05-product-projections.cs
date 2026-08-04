using System.Linq.Expressions;
using Warehouse.Application.Products;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

/// <summary>
/// The hand-written projection from the EF <see cref="Product"/> entity to
/// <see cref="ProductViewModel"/>, shared by the two grouping query handlers that previously
/// carried identical copies of it.
///
/// This deliberately does NOT match AutoMapper's <c>ProjectTo&lt;ProductViewModel&gt;</c>, which the
/// sibling handlers in this folder use: it falls back to <c>CreatedAt</c> for a null
/// <c>LastUpdatedAt</c> and to an empty string for a null <c>Description</c>, where AutoMapper
/// yields <c>DateTime.MinValue</c> and <c>null</c>. That divergence is pre-existing, is pinned by
/// tests on both sides, and is left exactly as it behaves today.
///
/// Kept as an <see cref="Expression"/> rather than a compiled delegate so EF Core still translates
/// it to SQL. Applied as <c>g.AsQueryable().Select(ToViewModel)</c> inside a grouping it produces
/// byte-identical SQL to the inlined literal it replaced, so no query shape changes.
/// </summary>
internal static class ProductProjections
{
    public static readonly Expression<Func<Product, ProductViewModel>> ToViewModel =
        p => new ProductViewModel
        {
            Id = p.Id,
            Name = p.Name,
            Sku = p.Sku,
            Description = p.Description ?? string.Empty,
            Price = p.Price,
            QuantityInStock = p.QuantityInStock,
            SupplierName = p.SupplierName,
            ExpiryDate = p.ExpiryDate,
            IsArchived = p.IsArchived,
            CreatedAt = p.CreatedAt,
            LastUpdatedAt = p.LastUpdatedAt ?? p.CreatedAt
        };
}
