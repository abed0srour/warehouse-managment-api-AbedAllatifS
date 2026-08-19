using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

public class GetProductsGroupedByExpiryYearQueryHandler : IRequestHandler<GetProductsGroupedByExpiryYearQuery, IEnumerable<ExpiryYearGroupDto>>
{
    private readonly WarehouseDbContext _context;

    public GetProductsGroupedByExpiryYearQueryHandler(WarehouseDbContext context) => _context = context;

    public async Task<IEnumerable<ExpiryYearGroupDto>> Handle(GetProductsGroupedByExpiryYearQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products
            .Where(p => p.ExpiryDate != null)
            .GroupBy(p => p.ExpiryDate!.Value.Year)
            .Select(g => new ExpiryYearGroupDto(
                g.Key,
                g.Count(),
                g.AsQueryable().Select(ProductProjections.ToViewModel).ToList()))
            .ToListAsync(cancellationToken);
    }
}
