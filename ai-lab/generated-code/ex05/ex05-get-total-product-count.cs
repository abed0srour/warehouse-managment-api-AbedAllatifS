using MediatR;
using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Products.Queries;
using Warehouse.Infrastructure.Data.EfModels;

namespace Warehouse.Infrastructure.Queries;

public class GetTotalProductCountQueryHandler : IRequestHandler<GetTotalProductCountQuery, int>
{
    private readonly WarehouseDbContext _context;

    public GetTotalProductCountQueryHandler(WarehouseDbContext context) => _context = context;

    public async Task<int> Handle(GetTotalProductCountQuery request, CancellationToken cancellationToken)
    {
        return await _context.Products.CountAsync(cancellationToken);
    }
}
