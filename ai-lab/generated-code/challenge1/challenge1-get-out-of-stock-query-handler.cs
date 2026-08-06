using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

public class GetOutOfStockProductsQueryHandler
    : IRequestHandler<GetOutOfStockProductsQuery, IEnumerable<OutOfStockProductDto>>
{
    private readonly WarehouseDbContext _context;

    public GetOutOfStockProductsQueryHandler(WarehouseDbContext context) => _context = context;

    public async Task<IEnumerable<OutOfStockProductDto>> Handle(
        GetOutOfStockProductsQuery request,
        CancellationToken cancellationToken)
    {
        return await _context.Products
            .AsNoTracking()
            .Where(p => p.QuantityInStock == 0 && !p.IsArchived)
            .OrderBy(p => p.Name)
            .Select(p => new OutOfStockProductDto(
                p.Id,
                p.Name,
                p.Sku,
                p.SupplierId,
                p.SupplierName,
                p.LastUpdatedAt))
            .ToListAsync(cancellationToken);
    }
}
