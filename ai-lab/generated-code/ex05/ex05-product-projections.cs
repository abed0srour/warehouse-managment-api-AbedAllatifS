using System.Linq.Expressions;
using Warehouse.Application.Products;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;
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
