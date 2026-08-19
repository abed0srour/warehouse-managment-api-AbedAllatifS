using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

public class GetProductsGroupedByExpiryAndCountryQueryHandler : IRequestHandler<GetProductsGroupedByExpiryAndCountryQuery, IEnumerable<ExpiryYearAndCountryGroupDto>>
{
    private readonly WarehouseDbContext _context;

    public GetProductsGroupedByExpiryAndCountryQueryHandler(WarehouseDbContext context) => _context = context;

    public async Task<IEnumerable<ExpiryYearAndCountryGroupDto>> Handle(GetProductsGroupedByExpiryAndCountryQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products
            .Where(p => p.ExpiryDate != null && p.Supplier != null)
            .GroupBy(p => new { Year = p.ExpiryDate!.Value.Year, gCountry = p.Supplier!.Country })
            .Select(g => new ExpiryYearAndCountryGroupDto(
                g.Key.Year,
                g.Key.gCountry,
                g.Count(),
                g.AsQueryable().Select(ProductProjections.ToViewModel).ToList()))
            .ToListAsync(cancellationToken);
    }
}
