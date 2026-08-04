using AutoMapper;
using AutoMapper.QueryableExtensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

public class GetProductsBySupplierQueryHandler : IRequestHandler<GetProductsBySupplierQuery, IEnumerable<ProductViewModel>>
{
    private readonly WarehouseDbContext _context;
    private readonly IMapper _mapper;

    public GetProductsBySupplierQueryHandler(WarehouseDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<IEnumerable<ProductViewModel>> Handle(GetProductsBySupplierQuery request, CancellationToken cancellationToken)
    {
        var query = _context.Products
            .Include(p => p.Supplier)
            .Where(p => p.SupplierName == request.SupplierName || (p.Supplier != null && p.Supplier.Name == request.SupplierName));

        query = request.SortOrder.ToLower() == "asc"
            ? query.OrderBy(p => p.CreatedAt)
            : query.OrderByDescending(p => p.CreatedAt);

        return await query.ProjectTo<ProductViewModel>(_mapper.ConfigurationProvider).ToListAsync(cancellationToken);
    }
}
